namespace helengine {
    /// <summary>
    /// Stores a stable reference from scene persistence metadata to a project or generated asset.
    /// </summary>
    public class SceneAssetReference {
        /// <summary>
        /// Gets or sets the source category used to resolve the asset reference.
        /// </summary>
        public SceneAssetReferenceSourceKind SourceKind { get; set; }

        /// <summary>
        /// Gets or sets the project-relative path for filesystem-backed assets.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Gets or sets the generated provider identifier for engine-backed assets.
        /// </summary>
        public string ProviderId { get; set; }

        /// <summary>
        /// Gets or sets the provider-local asset identifier.
        /// </summary>
        public string AssetId { get; set; }
    }
}
