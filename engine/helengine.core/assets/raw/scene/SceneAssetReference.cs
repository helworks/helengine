namespace helengine {
    /// <summary>
    /// Stores one validated stable reference from scene persistence metadata to a project or generated asset.
    /// </summary>
    public sealed class SceneAssetReference {
        /// <summary>
        /// Initializes one validated scene asset reference.
        /// </summary>
        /// <param name="sourceKind">Reference source kind.</param>
        /// <param name="relativePath">Stable relative path.</param>
        /// <param name="providerId">Generated provider id when applicable.</param>
        /// <param name="assetId">Stable authored asset id or generated asset id when applicable.</param>
        /// <param name="contentHash">Content hash for file-backed authored assets.</param>
        internal SceneAssetReference(SceneAssetReferenceSourceKind sourceKind, string relativePath, string providerId, string assetId, string contentHash) {
            SourceKind = sourceKind;
            RelativePath = relativePath ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            AssetId = assetId ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
        }

        /// <summary>
        /// Gets the source category used to resolve the asset reference.
        /// </summary>
        public SceneAssetReferenceSourceKind SourceKind { get; }

        /// <summary>
        /// Gets the project-relative path for filesystem-backed assets.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the generated provider identifier for engine-backed assets.
        /// </summary>
        public string ProviderId { get; }

        /// <summary>
        /// Gets the provider-local asset identifier.
        /// </summary>
        public string AssetId { get; }

        /// <summary>
        /// Gets the content hash used as the final recovery key for file-backed authored assets.
        /// </summary>
        public string ContentHash { get; }
    }
}
