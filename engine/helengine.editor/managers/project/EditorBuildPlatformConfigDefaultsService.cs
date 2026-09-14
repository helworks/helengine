using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Resolves and normalizes the persisted per-platform build selections (build, graphics, codegen, storage and media profiles plus
    /// their option maps) against the builder-provided platform selection metadata, keeping this business logic out of the build dialog UI.
    /// </summary>
    static class EditorBuildPlatformConfigDefaultsService {
        /// <summary>
        /// Ensures one platform configuration carries a valid environment, build profile, graphics profile, codegen profile,
        /// storage profile, media profile and fully seeded option maps for the supplied platform selection metadata.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to normalize in place.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform, or null when unavailable.</param>
        public static void EnsurePlatformSelectionDefaults(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                return;
            }

            EnsureEnvironmentDefault(platformConfig);
            if (selectionModel == null) {
                return;
            }

            PlatformBuildProfileDefinition previousBuildProfile = selectionModel.TryResolveBuildProfileExact(platformConfig.SelectedBuildProfileId);
            PlatformBuildProfileDefinition buildProfile = EditorBuildProfileDefaultResolver.ResolveBuildProfile(
                selectionModel,
                platformConfig.SelectedBuildProfileId,
                platformConfig.DebugBuild);
            if (buildProfile != null) {
                ApplyBuildProfileSelection(platformConfig, previousBuildProfile, buildProfile);
            }

            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platformConfig, buildProfile, selectionModel);
            if (graphicsProfile != null) {
                platformConfig.SelectedGraphicsProfileId = graphicsProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedGraphicsOptionValues, graphicsProfile.Settings);
            }

            PlatformCodegenProfileDefinition codegenProfile = ResolveCodegenProfile(platformConfig, buildProfile, selectionModel);
            if (codegenProfile != null) {
                platformConfig.SelectedCodegenProfileId = codegenProfile.ProfileId;
                platformConfig.SelectedCodegenOptionValues = EditorBuildProfileDefaultResolver.CreateEffectiveCodegenOptionValues(
                    platformConfig.SelectedCodegenOptionValues,
                    codegenProfile,
                    previousBuildProfile,
                    buildProfile);
            }

            PlatformStorageProfileDefinition storageProfile = ResolveStorageProfile(platformConfig, selectionModel);
            if (storageProfile != null) {
                platformConfig.SelectedStorageProfileId = storageProfile.ProfileId;
            }

            PlatformMediaProfileDefinition mediaProfile = ResolveMediaProfile(platformConfig, selectionModel);
            if (mediaProfile != null) {
                platformConfig.SelectedMediaProfileId = mediaProfile.ProfileId;
            }

            EnsureOptionValueMaps(platformConfig);
        }

        /// <summary>
        /// Seeds the nested build environment identifier from the currently selected build profile or debug flag when the platform has none.
        /// </summary>
        /// <param name="platformConfig">Platform configuration whose environment selection should be seeded.</param>
        static void EnsureEnvironmentDefault(EditorBuildPlatformConfigDocument platformConfig) {
            if (!string.IsNullOrWhiteSpace(platformConfig.SelectedEnvironmentId)) {
                return;
            }

            if (string.Equals(platformConfig.SelectedBuildProfileId, "debug", StringComparison.OrdinalIgnoreCase) || platformConfig.DebugBuild) {
                platformConfig.SelectedEnvironmentId = "debug";
            } else {
                platformConfig.SelectedEnvironmentId = "release";
            }
        }

        /// <summary>
        /// Stores the resolved build profile and re-points the bound graphics and codegen selections that still tracked the previous build profile.
        /// </summary>
        /// <param name="platformConfig">Platform configuration receiving the resolved selections.</param>
        /// <param name="previousBuildProfile">Build profile that was selected before this resolution pass, or null when none matched exactly.</param>
        /// <param name="buildProfile">Newly resolved build profile for the current build mode.</param>
        static void ApplyBuildProfileSelection(
            EditorBuildPlatformConfigDocument platformConfig,
            PlatformBuildProfileDefinition previousBuildProfile,
            PlatformBuildProfileDefinition buildProfile) {
            platformConfig.SelectedBuildProfileId = buildProfile.ProfileId;

            string previousGraphicsProfileId = string.Empty;
            string previousCodegenProfileId = string.Empty;
            if (previousBuildProfile != null) {
                if (previousBuildProfile.GraphicsProfileId != null) {
                    previousGraphicsProfileId = previousBuildProfile.GraphicsProfileId;
                }

                if (previousBuildProfile.CodegenProfileId != null) {
                    previousCodegenProfileId = previousBuildProfile.CodegenProfileId;
                }
            }

            string selectedGraphicsProfileId = platformConfig.SelectedGraphicsProfileId;
            EditorBuildProfileDefaultResolver.SynchronizeBoundProfileSelection(
                ref selectedGraphicsProfileId,
                previousGraphicsProfileId,
                buildProfile.GraphicsProfileId);
            platformConfig.SelectedGraphicsProfileId = selectedGraphicsProfileId;

            string selectedCodegenProfileId = platformConfig.SelectedCodegenProfileId;
            EditorBuildProfileDefaultResolver.SynchronizeBoundProfileSelection(
                ref selectedCodegenProfileId,
                previousCodegenProfileId,
                buildProfile.CodegenProfileId);
            platformConfig.SelectedCodegenProfileId = selectedCodegenProfileId;

            EnsureSettingDefaults(platformConfig.SelectedBuildOptionValues, buildProfile.Settings);
        }

        /// <summary>
        /// Replaces any missing option-value map with an empty dictionary so downstream binding never has to null-check them.
        /// </summary>
        /// <param name="platformConfig">Platform configuration whose option maps should exist.</param>
        static void EnsureOptionValueMaps(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig.SelectedBuildOptionValues == null) {
                platformConfig.SelectedBuildOptionValues = new Dictionary<string, string>();
            }

            if (platformConfig.SelectedGraphicsOptionValues == null) {
                platformConfig.SelectedGraphicsOptionValues = new Dictionary<string, string>();
            }

            if (platformConfig.SelectedCodegenOptionValues == null) {
                platformConfig.SelectedCodegenOptionValues = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Resolves the selected build profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform.</param>
        /// <returns>Resolved build profile metadata, or null when unavailable.</returns>
        public static PlatformBuildProfileDefinition ResolveBuildProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null || selectionModel == null) {
                return null;
            }

            return selectionModel.ResolveBuildProfile(platformConfig.SelectedBuildProfileId);
        }

        /// <summary>
        /// Resolves the selected graphics profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata supplying the inherited default, or null when unavailable.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform.</param>
        /// <returns>Resolved graphics profile metadata, or null when unavailable.</returns>
        public static PlatformGraphicsProfileDefinition ResolveGraphicsProfile(
            EditorBuildPlatformConfigDocument platformConfig,
            PlatformBuildProfileDefinition buildProfile,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null || selectionModel == null) {
                return null;
            }

            string graphicsProfileId = platformConfig.SelectedGraphicsProfileId;
            if (string.IsNullOrWhiteSpace(graphicsProfileId) && buildProfile != null) {
                graphicsProfileId = buildProfile.GraphicsProfileId;
            }

            return selectionModel.ResolveGraphicsProfile(graphicsProfileId);
        }

        /// <summary>
        /// Resolves the selected codegen profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata supplying the inherited default, or null when unavailable.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform.</param>
        /// <returns>Resolved codegen profile metadata, or null when unavailable.</returns>
        public static PlatformCodegenProfileDefinition ResolveCodegenProfile(
            EditorBuildPlatformConfigDocument platformConfig,
            PlatformBuildProfileDefinition buildProfile,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null || selectionModel == null) {
                return null;
            }

            string codegenProfileId = platformConfig.SelectedCodegenProfileId;
            if (string.IsNullOrWhiteSpace(codegenProfileId) && buildProfile != null) {
                codegenProfileId = buildProfile.CodegenProfileId;
            }

            return selectionModel.ResolveCodegenProfile(codegenProfileId);
        }

        /// <summary>
        /// Resolves the selected storage profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform.</param>
        /// <returns>Resolved storage profile metadata, or null when unavailable.</returns>
        public static PlatformStorageProfileDefinition ResolveStorageProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null || selectionModel == null) {
                return null;
            }

            return selectionModel.ResolveStorageProfile(platformConfig.SelectedStorageProfileId);
        }

        /// <summary>
        /// Resolves the selected media profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided metadata describing the profiles available for the platform.</param>
        /// <returns>Resolved media profile metadata, or null when unavailable.</returns>
        public static PlatformMediaProfileDefinition ResolveMediaProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null || selectionModel == null) {
                return null;
            }

            return selectionModel.ResolveMediaProfile(platformConfig.SelectedMediaProfileId);
        }

        /// <summary>
        /// Seeds missing option values from the supplied setting collection.
        /// </summary>
        /// <param name="values">Persisted option values.</param>
        /// <param name="settings">Builder-provided setting definitions.</param>
        public static void EnsureSettingDefaults(Dictionary<string, string> values, PlatformSettingDefinition[] settings) {
            if (values == null || settings == null) {
                return;
            }

            for (int index = 0; index < settings.Length; index++) {
                PlatformSettingDefinition setting = settings[index];
                if (setting == null || string.IsNullOrWhiteSpace(setting.SettingId)) {
                    continue;
                }

                if (!values.TryGetValue(setting.SettingId, out string existingValue) || string.IsNullOrWhiteSpace(existingValue)) {
                    values[setting.SettingId] = setting.DefaultValue;
                }
            }
        }
    }
}
