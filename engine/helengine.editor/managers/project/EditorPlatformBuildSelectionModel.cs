using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Captures the platform-provided build metadata the editor can use to populate build and profile UI.
    /// </summary>
    public sealed class EditorPlatformBuildSelectionModel {
        /// <summary>
        /// Initializes one selection model from the builder-provided platform definition.
        /// </summary>
        /// <param name="definition">Typed builder metadata.</param>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <param name="displayName">Human-readable platform name.</param>
        /// <param name="buildProfiles">Build profiles exposed by the builder.</param>
        /// <param name="graphicsProfiles">Graphics profiles exposed by the builder.</param>
        /// <param name="assetRequirements">Asset requirements exposed by the builder.</param>
        /// <param name="materialSchemas">Material schemas exposed by the builder.</param>
        /// <param name="componentCompatibilities">Component compatibility rules exposed by the builder.</param>
        /// <param name="codegenProfiles">Codegen profiles exposed by the builder.</param>
        /// <param name="storageProfiles">Storage/runtime profiles exposed by the builder.</param>
        /// <param name="mediaProfiles">Media profiles exposed by the builder.</param>
        public EditorPlatformBuildSelectionModel(
            PlatformDefinition definition,
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas,
            PlatformComponentCompatibilityDefinition[] componentCompatibilities,
            PlatformCodegenProfileDefinition[] codegenProfiles,
            PlatformStorageProfileDefinition[] storageProfiles,
            PlatformMediaProfileDefinition[] mediaProfiles) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id is required.", nameof(platformId));
            }
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Platform display name is required.", nameof(displayName));
            }
            if (buildProfiles == null) {
                throw new ArgumentNullException(nameof(buildProfiles));
            }
            if (graphicsProfiles == null) {
                throw new ArgumentNullException(nameof(graphicsProfiles));
            }
            if (assetRequirements == null) {
                throw new ArgumentNullException(nameof(assetRequirements));
            }
            if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas));
            }
            if (componentCompatibilities == null) {
                throw new ArgumentNullException(nameof(componentCompatibilities));
            }
            if (codegenProfiles == null) {
                throw new ArgumentNullException(nameof(codegenProfiles));
            }
            if (storageProfiles == null) {
                throw new ArgumentNullException(nameof(storageProfiles));
            }
            if (mediaProfiles == null) {
                throw new ArgumentNullException(nameof(mediaProfiles));
            }

            PlatformId = platformId;
            DisplayName = displayName;
            BuildProfiles = buildProfiles;
            GraphicsProfiles = graphicsProfiles;
            AssetRequirements = assetRequirements;
            MaterialSchemas = materialSchemas;
            ComponentCompatibilities = componentCompatibilities;
            CodegenProfiles = codegenProfiles;
            StorageProfiles = storageProfiles;
            MediaProfiles = mediaProfiles;
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
        /// Gets the build profiles exposed by the platform builder.
        /// </summary>
        public PlatformBuildProfileDefinition[] BuildProfiles { get; }

        /// <summary>
        /// Gets the graphics profiles exposed by the platform builder.
        /// </summary>
        public PlatformGraphicsProfileDefinition[] GraphicsProfiles { get; }

        /// <summary>
        /// Gets the asset requirements exposed by the platform builder.
        /// </summary>
        public PlatformAssetRequirementDefinition[] AssetRequirements { get; }

        /// <summary>
        /// Gets the material schemas exposed by the platform builder.
        /// </summary>
        public PlatformMaterialSchemaDefinition[] MaterialSchemas { get; }

        /// <summary>
        /// Gets the component compatibility rules exposed by the platform builder.
        /// </summary>
        public PlatformComponentCompatibilityDefinition[] ComponentCompatibilities { get; }

        /// <summary>
        /// Gets the codegen profiles exposed by the platform builder.
        /// </summary>
        public PlatformCodegenProfileDefinition[] CodegenProfiles { get; }

        /// <summary>
        /// Gets the storage/runtime profiles exposed by the platform builder.
        /// </summary>
        public PlatformStorageProfileDefinition[] StorageProfiles { get; }

        /// <summary>
        /// Gets the media profiles exposed by the platform builder.
        /// </summary>
        public PlatformMediaProfileDefinition[] MediaProfiles { get; }

        /// <summary>
        /// Gets the underlying platform definition exposed by the builder.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Creates one selection model from a builder-provided platform definition.
        /// </summary>
        /// <param name="definition">Typed builder metadata.</param>
        /// <returns>Selection model backed by the supplied definition.</returns>
        public static EditorPlatformBuildSelectionModel From(PlatformDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return new EditorPlatformBuildSelectionModel(
                definition,
                definition.PlatformId,
                definition.DisplayName,
                definition.BuildProfiles,
                definition.GraphicsProfiles,
                definition.AssetRequirements,
                definition.MaterialSchemas,
                definition.ComponentCompatibilities,
                definition.CodegenProfiles,
                definition.StorageProfiles,
                definition.MediaProfiles);
        }

        /// <summary>
        /// Resolves one build profile by id, falling back to the first available build profile when needed.
        /// </summary>
        /// <param name="profileId">Requested build profile identifier.</param>
        /// <returns>Matching build profile or the first available build profile.</returns>
        public PlatformBuildProfileDefinition ResolveBuildProfile(string profileId) {
            if (BuildProfiles.Length == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(profileId)) {
                for (int index = 0; index < BuildProfiles.Length; index++) {
                    PlatformBuildProfileDefinition buildProfile = BuildProfiles[index];
                    if (string.Equals(buildProfile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) {
                        return buildProfile;
                    }
                }
            }

            return BuildProfiles[0];
        }

        /// <summary>
        /// Resolves one graphics profile by id, falling back to the first available graphics profile when needed.
        /// </summary>
        /// <param name="profileId">Requested graphics profile identifier.</param>
        /// <returns>Matching graphics profile or the first available graphics profile.</returns>
        public PlatformGraphicsProfileDefinition ResolveGraphicsProfile(string profileId) {
            if (GraphicsProfiles.Length == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(profileId)) {
                for (int index = 0; index < GraphicsProfiles.Length; index++) {
                    PlatformGraphicsProfileDefinition graphicsProfile = GraphicsProfiles[index];
                    if (string.Equals(graphicsProfile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) {
                        return graphicsProfile;
                    }
                }
            }

            return GraphicsProfiles[0];
        }

        /// <summary>
        /// Resolves one build profile's setting collection.
        /// </summary>
        /// <param name="profileId">Requested build profile identifier.</param>
        /// <returns>Build profile settings or an empty array when unavailable.</returns>
        public PlatformSettingDefinition[] ResolveBuildProfileSettings(string profileId) {
            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(profileId);
            return buildProfile?.Settings ?? Array.Empty<PlatformSettingDefinition>();
        }

        /// <summary>
        /// Resolves one graphics profile's setting collection.
        /// </summary>
        /// <param name="profileId">Requested graphics profile identifier.</param>
        /// <returns>Graphics profile settings or an empty array when unavailable.</returns>
        public PlatformSettingDefinition[] ResolveGraphicsProfileSettings(string profileId) {
            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(profileId);
            return graphicsProfile?.Settings ?? Array.Empty<PlatformSettingDefinition>();
        }

        /// <summary>
        /// Resolves the material schemas available to one graphics profile.
        /// </summary>
        /// <param name="graphicsProfileId">Requested graphics profile identifier.</param>
        /// <returns>Material schemas that apply to the resolved graphics profile.</returns>
        public PlatformMaterialSchemaDefinition[] ResolveMaterialSchemas(string graphicsProfileId) {
            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(graphicsProfileId);

            if (graphicsProfile == null) {
                return [.. MaterialSchemas];
            }

            List<PlatformMaterialSchemaDefinition> matchingSchemas = [];
            for (int index = 0; index < MaterialSchemas.Length; index++) {
                PlatformMaterialSchemaDefinition materialSchema = MaterialSchemas[index];
                if (materialSchema.GraphicsProfileIds.Length == 0 ||
                    Array.Exists(
                        materialSchema.GraphicsProfileIds,
                        profileId => string.Equals(profileId, graphicsProfile.ProfileId, StringComparison.OrdinalIgnoreCase))) {
                    matchingSchemas.Add(materialSchema);
                }
            }

            return [.. matchingSchemas];
        }

        /// <summary>
        /// Resolves one codegen profile by id, falling back to the first available codegen profile when needed.
        /// </summary>
        /// <param name="profileId">Requested codegen profile identifier.</param>
        /// <returns>Matching codegen profile or the first available codegen profile.</returns>
        public PlatformCodegenProfileDefinition ResolveCodegenProfile(string profileId) {
            if (CodegenProfiles.Length == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(profileId)) {
                for (int index = 0; index < CodegenProfiles.Length; index++) {
                    PlatformCodegenProfileDefinition codegenProfile = CodegenProfiles[index];
                    if (string.Equals(codegenProfile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) {
                        return codegenProfile;
                    }
                }
            }

            return CodegenProfiles[0];
        }

        /// <summary>
        /// Resolves one media profile by id, falling back to the first available media profile when needed.
        /// </summary>
        /// <param name="profileId">Requested media profile identifier.</param>
        /// <returns>Matching media profile or the first available media profile.</returns>
        public PlatformMediaProfileDefinition ResolveMediaProfile(string profileId) {
            if (MediaProfiles.Length == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(profileId)) {
                for (int index = 0; index < MediaProfiles.Length; index++) {
                    PlatformMediaProfileDefinition mediaProfile = MediaProfiles[index];
                    if (string.Equals(mediaProfile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) {
                        return mediaProfile;
                    }
                }
            }

            return MediaProfiles[0];
        }

        /// <summary>
        /// Resolves one codegen profile's setting collection.
        /// </summary>
        /// <param name="profileId">Requested codegen profile identifier.</param>
        /// <returns>Codegen profile settings or an empty array when unavailable.</returns>
        public PlatformSettingDefinition[] ResolveCodegenProfileSettings(string profileId) {
            PlatformCodegenProfileDefinition codegenProfile = ResolveCodegenProfile(profileId);
            return codegenProfile?.Settings ?? Array.Empty<PlatformSettingDefinition>();
        }

        /// <summary>
        /// Resolves one storage profile by id, falling back to the first available storage profile when needed.
        /// </summary>
        /// <param name="profileId">Requested storage profile identifier.</param>
        /// <returns>Matching storage profile or the first available storage profile.</returns>
        public PlatformStorageProfileDefinition ResolveStorageProfile(string profileId) {
            if (StorageProfiles.Length == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(profileId)) {
                for (int index = 0; index < StorageProfiles.Length; index++) {
                    PlatformStorageProfileDefinition storageProfile = StorageProfiles[index];
                    if (string.Equals(storageProfile.ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) {
                        return storageProfile;
                    }
                }
            }

            return StorageProfiles[0];
        }

        /// <summary>
        /// Resolves one storage profile's setting collection.
        /// </summary>
        /// <param name="profileId">Requested storage profile identifier.</param>
        /// <returns>Storage profile settings or an empty array when unavailable.</returns>
        public PlatformSettingDefinition[] ResolveStorageProfileSettings(string profileId) {
            return Array.Empty<PlatformSettingDefinition>();
        }
    }
}
