namespace helengine.editor {
    /// <summary>
    /// Describes one native asset destination produced by the project authoring session.
    /// </summary>
    public sealed class EditorAssetWriteResult {
        /// <summary>
        /// Initializes one asset write result.
        /// </summary>
        /// <param name="relativePath">Normalized path relative to the project assets root.</param>
        /// <param name="fullPath">Canonical absolute destination path.</param>
        /// <param name="assetId">Final embedded authoring identity.</param>
        /// <param name="contentHash">Current recovery content hash.</param>
        /// <param name="disposition">Destination write disposition.</param>
        /// <param name="preservedExistingIdentity">Whether an existing destination identity was preserved.</param>
        public EditorAssetWriteResult(
            string relativePath,
            string fullPath,
            string assetId,
            string contentHash,
            EditorAssetWriteDisposition disposition,
            bool preservedExistingIdentity) {
            RelativePath = relativePath ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            AssetId = assetId ?? string.Empty;
            ContentHash = contentHash ?? string.Empty;
            Disposition = disposition;
            PreservedExistingIdentity = preservedExistingIdentity;
        }

        /// <summary>
        /// Gets the normalized assets-relative destination path.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the canonical absolute destination path.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the final embedded authoring identity.
        /// </summary>
        public string AssetId { get; }

        /// <summary>
        /// Gets the content hash reported for the destination.
        /// </summary>
        public string ContentHash { get; }

        /// <summary>
        /// Gets the destination write disposition.
        /// </summary>
        public EditorAssetWriteDisposition Disposition { get; }

        /// <summary>
        /// Gets a value indicating whether the destination's prior identity was retained.
        /// </summary>
        public bool PreservedExistingIdentity { get; }
    }
}
