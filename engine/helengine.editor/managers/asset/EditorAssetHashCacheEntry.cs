namespace helengine.editor {
    /// <summary>
    /// Stores one authored asset fingerprint and its computed content hash.
    /// </summary>
    public sealed class EditorAssetHashCacheEntry {
        /// <summary>
        /// Gets or sets the normalized path relative to the assets root.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source file length at hash time.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// Gets or sets the source file's last-write UTC ticks at hash time.
        /// </summary>
        public long LastWriteUtcTicks { get; set; }

        /// <summary>
        /// Gets or sets the prefixed lowercase content hash.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;
    }
}
