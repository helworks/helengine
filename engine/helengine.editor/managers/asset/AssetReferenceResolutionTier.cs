namespace helengine.editor {
    /// <summary>
    /// Identifies the ordered key used to recover an authored asset reference.
    /// </summary>
    public enum AssetReferenceResolutionTier {
        /// <summary>Recovered through the stable authored UUID.</summary>
        AssetId,
        /// <summary>Recovered through the saved assets-relative path.</summary>
        Path,
        /// <summary>Recovered through the content hash.</summary>
        ContentHash
    }
}
