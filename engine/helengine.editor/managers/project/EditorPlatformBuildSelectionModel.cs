using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Captures the platform-provided build metadata the editor can use to populate build and profile UI.
    /// </summary>
    public sealed class EditorPlatformBuildSelectionModel {
        /// <summary>
        /// Initializes one selection model from the builder-provided platform definition.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <param name="displayName">Human-readable platform name.</param>
        /// <param name="buildProfiles">Build profiles exposed by the builder.</param>
        /// <param name="graphicsProfiles">Graphics profiles exposed by the builder.</param>
        /// <param name="assetRequirements">Asset requirements exposed by the builder.</param>
        public EditorPlatformBuildSelectionModel(
            string platformId,
            string displayName,
            PlatformBuildProfileDefinition[] buildProfiles,
            PlatformGraphicsProfileDefinition[] graphicsProfiles,
            PlatformAssetRequirementDefinition[] assetRequirements) {
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

            PlatformId = platformId;
            DisplayName = displayName;
            BuildProfiles = buildProfiles;
            GraphicsProfiles = graphicsProfiles;
            AssetRequirements = assetRequirements;
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
        /// Creates one selection model from a builder-provided platform definition.
        /// </summary>
        /// <param name="definition">Typed builder metadata.</param>
        /// <returns>Selection model backed by the supplied definition.</returns>
        public static EditorPlatformBuildSelectionModel From(PlatformDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            return new EditorPlatformBuildSelectionModel(
                definition.PlatformId,
                definition.DisplayName,
                definition.BuildProfiles,
                definition.GraphicsProfiles,
                definition.AssetRequirements);
        }
    }
}
