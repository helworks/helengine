namespace helengine.editor {
    /// <summary>
    /// Stores the stable identity and former identity aliases for one authored asset sidecar.
    /// </summary>
    public sealed class AssetIdentityMetadataDocument {
        /// <summary>
        /// Gets or sets the sidecar schema version.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the current stable authored asset UUID.
        /// </summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets former stable UUIDs retained after deterministic collision repair.
        /// </summary>
        public List<string> FormerAssetIds { get; set; } = new List<string>();
    }
}
