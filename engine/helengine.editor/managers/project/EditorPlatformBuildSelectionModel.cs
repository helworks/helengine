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
        public EditorPlatformBuildSelectionModel(
            PlatformDefinition definition,
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements,
            PlatformMaterialSchemaDefinition[] materialSchemas) {
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

            PlatformId = platformId;
            DisplayName = displayName;
            BuildProfiles = buildProfiles;
            GraphicsProfiles = graphicsProfiles;
            AssetRequirements = assetRequirements;
            MaterialSchemas = materialSchemas;
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
                definition.MaterialSchemas);
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
    }
}
