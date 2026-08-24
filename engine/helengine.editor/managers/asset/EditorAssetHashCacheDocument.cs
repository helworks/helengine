namespace helengine.editor {
    /// <summary>
    /// Stores disposable cached fingerprints for authored asset content hashes.
    /// </summary>
    public sealed class EditorAssetHashCacheDocument {
        /// <summary>
        /// Gets or sets cached file fingerprint entries.
        /// </summary>
        public List<EditorAssetHashCacheEntry> Entries { get; set; } = new List<EditorAssetHashCacheEntry>();
    }
}
