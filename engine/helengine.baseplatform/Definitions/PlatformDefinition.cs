namespace helengine.baseplatform.Definitions {

    /// <summary>
    /// Describes the typed platform metadata a builder exposes to the editor.
    /// </summary>
    public class PlatformDefinition {
        /// <summary>Gets or sets runtime setting contracts; platform builders may add or replace definitions before publishing their metadata.</summary>
        public PlatformRuntimeSettingDefinition[] RuntimeSettings { get; set; } = CommonPlatformRuntimeSettings.CreateDefinitions();
        /// <summary>
        /// Initializes one platform definition.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <param name="displayName">Human-readable platform name.</param>
        /// <param name="buildProfiles">Build profiles exposed by the platform.</param>
        /// <param name="graphicsProfiles">Graphics profiles exposed by the platform.</param>
        /// <param name="assetRequirements">Asset requirements exposed by the platform.</param>
        /// <param name="materialSchemas">Material authoring schemas exposed by the platform.</param>
        /// <param name="componentSupportRules">Component support rules exposed by the platform.</param>
        /// <param name="codegenProfiles">Codegen profiles exposed by the platform.</param>
        /// <param name="storageProfiles">Storage/runtime profiles exposed by the platform.</param>
        /// <param name="mediaProfiles">Media profiles exposed by the platform.</param>
        /// <param name="runtimeGenerationContract">Cross-platform runtime-generation behavior exposed by the platform.</param>
        /// <param name="hostDebugCapability">Cross-platform host-debug capability metadata exposed by the platform.</param>
        /// <param name="assetCookCapabilities">Generic asset-kind cook capabilities exposed by the platform.</param>
        /// <param name="componentMemberDefinitions">Platform-specific synthetic component members exposed to editor and runtime packaging flows.</param>
        /// <param name="playerCapability">Editor Play capability exposed by the platform; null means the platform does not support Play.</param>
        /// <param name="videoModeCapability">Video modes the console produces; null when the platform has none (its output is not mode-based).</param>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformStorageProfileDefinition[] storageProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles,
            RuntimeGenerationContract runtimeGenerationContract = null,
            PlatformHostDebugCapability hostDebugCapability = null,
            PlatformAssetCookCapabilityDefinition[] assetCookCapabilities = null,
            PlatformComponentMemberDefinition[] componentMemberDefinitions = null,
            PlatformPlayerCapability playerCapability = null,
            PlatformVideoModeCapability videoModeCapability = null) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id is required.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Platform display name is required.", nameof(displayName));
            } else if (buildProfiles == null) {
                throw new ArgumentNullException(nameof(buildProfiles), "Build profiles are required.");
            } else if (graphicsProfiles == null) {
                throw new ArgumentNullException(nameof(graphicsProfiles), "Graphics profiles are required.");
            } else if (assetRequirements == null) {
                throw new ArgumentNullException(nameof(assetRequirements), "Asset requirements are required.");
            } else if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas), "Material schemas are required.");
            } else if (componentSupportRules == null) {
                throw new ArgumentNullException(nameof(componentSupportRules), "Component support rules are required.");
            } else if (codegenProfiles == null) {
                throw new ArgumentNullException(nameof(codegenProfiles), "Codegen profiles are required.");
            } else if (storageProfiles == null) {
                throw new ArgumentNullException(nameof(storageProfiles), "Storage profiles are required.");
            } else if (mediaProfiles == null) {
                throw new ArgumentNullException(nameof(mediaProfiles), "Media profiles are required.");
            } else if (Array.Exists(buildProfiles, buildProfile => buildProfile == null)) {
                throw new ArgumentException("Build profiles cannot contain null entries.", nameof(buildProfiles));
            } else if (Array.Exists(graphicsProfiles, graphicsProfile => graphicsProfile == null)) {
                throw new ArgumentException("Graphics profiles cannot contain null entries.", nameof(graphicsProfiles));
            } else if (Array.Exists(assetRequirements, assetRequirement => assetRequirement == null)) {
                throw new ArgumentException("Asset requirements cannot contain null entries.", nameof(assetRequirements));
            } else if (Array.Exists(materialSchemas, materialSchema => materialSchema == null)) {
                throw new ArgumentException("Material schemas cannot contain null entries.", nameof(materialSchemas));
            } else if (Array.Exists(componentSupportRules, componentSupportRule => componentSupportRule == null)) {
                throw new ArgumentException("Component support rules cannot contain null entries.", nameof(componentSupportRules));
            } else if (Array.Exists(componentSupportRules, componentSupportRule => !IsEngineComponentTypeId(componentSupportRule.ComponentTypeId))) {
                throw new ArgumentException("Component support rules may only name engine components (helengine.*). Game script components reach every platform through the generic reflected transform, so a platform definition must not depend on one project's types.", nameof(componentSupportRules));
            } else if (Array.Exists(codegenProfiles, codegenProfile => codegenProfile == null)) {
                throw new ArgumentException("Codegen profiles cannot contain null entries.", nameof(codegenProfiles));
            } else if (Array.Exists(storageProfiles, storageProfile => storageProfile == null)) {
                throw new ArgumentException("Storage profiles cannot contain null entries.", nameof(storageProfiles));
            } else if (Array.Exists(mediaProfiles, mediaProfile => mediaProfile == null)) {
                throw new ArgumentException("Media profiles cannot contain null entries.", nameof(mediaProfiles));
            } else if (assetCookCapabilities != null && Array.Exists(assetCookCapabilities, assetCookCapability => assetCookCapability == null)) {
                throw new ArgumentException("Asset cook capabilities cannot contain null entries.", nameof(assetCookCapabilities));
            } else if (componentMemberDefinitions != null && Array.Exists(componentMemberDefinitions, definition => definition == null)) {
                throw new ArgumentException("Component member definitions cannot contain null entries.", nameof(componentMemberDefinitions));
            }

            PlatformId = platformId;
            DisplayName = displayName;
            BuildProfiles = [.. buildProfiles];
            GraphicsProfiles = [.. graphicsProfiles];
            AssetRequirements = [.. assetRequirements];
            MaterialSchemas = [.. materialSchemas];
            ComponentSupportRules = [.. componentSupportRules];
            CodegenProfiles = [.. codegenProfiles];
            StorageProfiles = [.. storageProfiles];
            MediaProfiles = [.. mediaProfiles];
            RuntimeGenerationContract = runtimeGenerationContract ?? RuntimeGenerationContract.CreateDefault();
            HostDebugCapability = hostDebugCapability ?? PlatformHostDebugCapability.CreateDefault();
            AssetCookCapabilities = assetCookCapabilities == null ? Array.Empty<PlatformAssetCookCapabilityDefinition>() : [.. assetCookCapabilities];
            ComponentMemberDefinitions = componentMemberDefinitions == null ? Array.Empty<PlatformComponentMemberDefinition>() : [.. componentMemberDefinitions];
            PlayerCapability = playerCapability ?? PlatformPlayerCapability.CreateUnsupported();
            ValidateProjectPlaySettings(PlayerCapability, GraphicsProfiles);
            VideoModeCapability = videoModeCapability;
            ValidateVideoModes(platformId, PlayerCapability, GraphicsProfiles, videoModeCapability);
        }

        /// <summary>
        /// Initializes one platform definition without any media profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformStorageProfileDefinition[] storageProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles,
            RuntimeGenerationContract runtimeGenerationContract)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                componentSupportRules,
                codegenProfiles,
                storageProfiles,
                mediaProfiles,
                runtimeGenerationContract,
                null) {
        }

        /// <summary>
        /// Initializes one platform definition while preserving the pre-asset-cook-capability constructor shape used by existing platform builders.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <param name="displayName">Human-readable platform name.</param>
        /// <param name="buildProfiles">Build profiles exposed by the platform.</param>
        /// <param name="graphicsProfiles">Graphics profiles exposed by the platform.</param>
        /// <param name="assetRequirements">Asset requirements exposed by the platform.</param>
        /// <param name="materialSchemas">Material authoring schemas exposed by the platform.</param>
        /// <param name="componentSupportRules">Component support rules exposed by the platform.</param>
        /// <param name="codegenProfiles">Codegen profiles exposed by the platform.</param>
        /// <param name="storageProfiles">Storage/runtime profiles exposed by the platform.</param>
        /// <param name="mediaProfiles">Media profiles exposed by the platform.</param>
        /// <param name="runtimeGenerationContract">Cross-platform runtime-generation behavior exposed by the platform.</param>
        /// <param name="hostDebugCapability">Cross-platform host-debug capability metadata exposed by the platform.</param>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformStorageProfileDefinition[] storageProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles,
            RuntimeGenerationContract runtimeGenerationContract,
            PlatformHostDebugCapability hostDebugCapability)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                componentSupportRules,
                codegenProfiles,
                storageProfiles,
                mediaProfiles,
                runtimeGenerationContract,
                hostDebugCapability,
                null) {
        }

        /// <summary>
        /// Initializes one platform definition without any media profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                componentSupportRules,
                codegenProfiles,
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>()) {
        }

        /// <summary>
        /// Initializes one platform definition without any storage profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                componentSupportRules,
                codegenProfiles,
                Array.Empty<PlatformStorageProfileDefinition>(),
                mediaProfiles) {
        }

        /// <summary>
        /// Initializes one platform definition without any codegen profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentSupportRule[] componentSupportRules)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                componentSupportRules,
                Array.Empty<PlatformCodegenProfileDefinition>()) {
        }

        /// <summary>
        /// Initializes one platform definition without any material schemas.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformStorageProfileDefinition[] storageProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                componentSupportRules,
                codegenProfiles,
                storageProfiles,
                mediaProfiles) {
        }

        /// <summary>
        /// Initializes one platform definition without any material schemas or media profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                componentSupportRules,
                codegenProfiles,
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>()) {
        }

        /// <summary>
        /// Initializes one platform definition without any material schemas or storage profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformComponentSupportRule[] componentSupportRules,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                componentSupportRules,
                codegenProfiles,
                Array.Empty<PlatformStorageProfileDefinition>(),
                mediaProfiles) {
        }

        /// <summary>
        /// Initializes one platform definition without any material schemas or codegen profiles.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformComponentSupportRule[] componentSupportRules)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                componentSupportRules,
                Array.Empty<PlatformCodegenProfileDefinition>()) {
        }

        /// <summary>
        /// Initializes one platform definition with only material schemas and no support-rule/codegen/storage/media metadata.
        /// </summary>
        public PlatformDefinition(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas)
            : this(
                platformId,
                displayName,
                buildProfiles,
                graphicsProfiles,
                assetRequirements,
                materialSchemas,
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>()) {
        }

        /// <summary>
        /// Gets the stable platform identifier.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the human-readable platform name.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the build profiles exposed by the platform.
        /// </summary>
        public PlatformBuildProfileDefinition[] BuildProfiles { get; }

        /// <summary>
        /// Gets the graphics profiles exposed by the platform.
        /// </summary>
        public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }

        /// <summary>
        /// Gets the asset requirements exposed by the platform.
        /// </summary>
        public PlatformAssetRequirementDefinition[] AssetRequirements { get; }

        /// <summary>
        /// Gets the material authoring schemas exposed by the platform.
        /// </summary>
        public PlatformMaterialSchemaDefinition[] MaterialSchemas { get; }

        /// <summary>
        /// Gets the component support rules exposed by the platform.
        /// </summary>
        public PlatformComponentSupportRule[] ComponentSupportRules { get; }

        /// <summary>
        /// Ensures every Play setting that may hold <see cref="PlayerCommonPlaySettings.ProjectValue"/> names a graphics option
        /// declared by every graphics profile, because the editor resolves <c>project</c> to that option when Play launches.
        /// </summary>
        /// <param name="playerCapability">Play capability whose settings are checked.</param>
        /// <param name="graphicsProfiles">Graphics profiles the project may select.</param>
        /// <exception cref="ArgumentException">Thrown when a graphics profile lacks the option a Play setting follows.</exception>
        static void ValidateProjectPlaySettings(PlatformPlayerCapability playerCapability, PlatformGraphicsProfileDefinition[] graphicsProfiles) {
            for (int settingIndex = 0; settingIndex < playerCapability.PlaySettings.Length; settingIndex++) {
                PlatformSettingDefinition playSetting = playerCapability.PlaySettings[settingIndex];
                bool canFollowProject = Array.IndexOf(playSetting.AllowedValues, PlayerCommonPlaySettings.ProjectValue) >= 0
                    || string.Equals(playSetting.DefaultValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal);
                if (!canFollowProject) {
                    continue;
                }

                for (int profileIndex = 0; profileIndex < graphicsProfiles.Length; profileIndex++) {
                    PlatformGraphicsProfileDefinition profile = graphicsProfiles[profileIndex];
                    if (!Array.Exists(profile.Settings, setting => string.Equals(setting.SettingId, playSetting.SettingId, StringComparison.Ordinal))) {
                        throw new ArgumentException(
                            $"Play setting '{playSetting.SettingId}' can follow the project, but graphics profile '{profile.ProfileId}' declares no '{playSetting.SettingId}' option.",
                            "playerCapability");
                    }
                }
            }
        }

        /// <summary>
        /// Checks that video modes agree with the graphics profiles and the Play declarations, and that a Play platform has
        /// exactly one frame-rate source. Every message starts with the platform id, because the editor loads every
        /// installed builder and the author must see which platform's declaration is wrong.
        /// </summary>
        /// <param name="platformId">Platform whose declarations are checked; named in every message.</param>
        /// <param name="playerCapability">Play capability.</param>
        /// <param name="graphicsProfiles">Graphics profiles.</param>
        /// <param name="videoModes">Declared video modes, or null.</param>
        /// <exception cref="ArgumentException">Thrown when the declarations disagree.</exception>
        static void ValidateVideoModes(string platformId, PlatformPlayerCapability playerCapability, PlatformGraphicsProfileDefinition[] graphicsProfiles, PlatformVideoModeCapability videoModes) {
            bool hasModes = videoModes != null;
            if (playerCapability.SupportsPlay && hasModes == (playerCapability.TargetFrameRate > 0d)) {
                string declaredRate = playerCapability.TargetFrameRate.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                string declaredModes = hasModes ? "and video modes" : "and no video modes";
                throw new ArgumentException(
                    $"Platform '{platformId}' declares a Play capability frame rate of {declaredRate} {declaredModes}. A platform that supports Play takes its player frame rate from exactly one source: a positive Play capability frame rate, or video modes with a Play capability frame rate of 0.",
                    "playerCapability");
            } else if (!hasModes) {
                return;
            }

            string[] modeIds = videoModes.GetModeIds();
            for (int profileIndex = 0; profileIndex < graphicsProfiles.Length; profileIndex++) {
                PlatformGraphicsProfileDefinition profile = graphicsProfiles[profileIndex];
                PlatformSettingDefinition setting = Array.Find(profile.Settings, candidate => string.Equals(candidate.SettingId, videoModes.SettingId, StringComparison.Ordinal));
                if (setting == null) {
                    throw new ArgumentException($"Platform '{platformId}': graphics profile '{profile.ProfileId}' declares no '{videoModes.SettingId}' setting to select the video mode.", "videoModeCapability");
                } else if (setting.SettingKind != PlatformSettingKind.Choice || !setting.AllowedValues.SequenceEqual(modeIds, StringComparer.Ordinal)) {
                    throw new ArgumentException($"Platform '{platformId}': graphics profile '{profile.ProfileId}' setting '{videoModes.SettingId}' must be a choice of exactly: {string.Join(", ", modeIds)}.", "videoModeCapability");
                } else if (Array.IndexOf(modeIds, setting.DefaultValue) < 0) {
                    throw new ArgumentException($"Platform '{platformId}': graphics profile '{profile.ProfileId}' setting '{videoModes.SettingId}' defaults to '{setting.DefaultValue}', which is not a video mode.", "videoModeCapability");
                }
            }

            if (!playerCapability.SupportsPlay) {
                return;
            }

            string[] playValues = new string[modeIds.Length + 1];
            playValues[0] = PlayerCommonPlaySettings.ProjectValue;
            Array.Copy(modeIds, 0, playValues, 1, modeIds.Length);
            PlatformSettingDefinition playSetting = playerCapability.FindPlaySetting(videoModes.SettingId);
            if (playSetting == null) {
                throw new ArgumentException(
                    $"Platform '{platformId}' supports Play and declares video modes, but declares no Play setting '{videoModes.SettingId}' to choose the video mode Play renders.",
                    "videoModeCapability");
            } else if (playSetting.SettingKind != PlatformSettingKind.Choice) {
                throw new ArgumentException(
                    $"Platform '{platformId}': Play setting '{videoModes.SettingId}' is a {playSetting.SettingKind} setting but must be a choice.",
                    "videoModeCapability");
            } else if (!playSetting.AllowedValues.SequenceEqual(playValues, StringComparer.Ordinal)) {
                throw new ArgumentException(
                    $"Platform '{platformId}': Play setting '{videoModes.SettingId}' must offer exactly, in order: {string.Join(", ", playValues)}.",
                    "videoModeCapability");
            } else if (!string.Equals(playSetting.DefaultValue, PlayerCommonPlaySettings.ProjectValue, StringComparison.Ordinal)) {
                throw new ArgumentException(
                    $"Platform '{platformId}': Play setting '{videoModes.SettingId}' defaults to '{playSetting.DefaultValue}' but must default to '{PlayerCommonPlaySettings.ProjectValue}'.",
                    "videoModeCapability");
            }
        }

        /// <summary>
        /// Returns true when a component type id belongs to the engine (the <c>helengine.</c> namespace). Anything else
        /// is a game project's script component, which no platform definition may name.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        public static bool IsEngineComponentTypeId(string componentTypeId) {
            return !string.IsNullOrWhiteSpace(componentTypeId)
                && componentTypeId.StartsWith("helengine.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the codegen profiles exposed by the platform.
        /// </summary>
        public PlatformCodegenProfileDefinition[] CodegenProfiles { get; }

        /// <summary>
        /// Gets the storage/runtime profiles exposed by the platform.
        /// </summary>
        public PlatformStorageProfileDefinition[] StorageProfiles { get; }

        /// <summary>
        /// Gets the media profiles exposed by the platform.
        /// </summary>
        public PlatformMediaProfileDefinition[] MediaProfiles { get; }

        /// <summary>
        /// Gets the cross-platform runtime-generation behavior exposed by the platform.
        /// </summary>
        public RuntimeGenerationContract RuntimeGenerationContract { get; }

        /// <summary>
        /// Gets the cross-platform host-debug capability metadata exposed by the platform.
        /// </summary>
        public PlatformHostDebugCapability HostDebugCapability { get; }

        /// <summary>
        /// Gets the generic asset-kind cook capabilities exposed by the platform.
        /// </summary>
        public PlatformAssetCookCapabilityDefinition[] AssetCookCapabilities { get; }

        /// <summary>
        /// Gets the platform-specific synthetic component members exposed to editor and runtime packaging flows.
        /// </summary>
        public PlatformComponentMemberDefinition[] ComponentMemberDefinitions { get; }

        /// <summary>
        /// Gets the editor Play capability exposed by the platform; unsupported unless the platform opts in.
        /// </summary>
        public PlatformPlayerCapability PlayerCapability { get; }

        /// <summary>
        /// Gets the video modes the console produces, or null when the platform declares none.
        /// </summary>
        public PlatformVideoModeCapability VideoModeCapability { get; }

        /// <summary>
        /// Gets whether the platform declares video modes.
        /// </summary>
        public bool HasVideoModes => VideoModeCapability != null;
    }


}
