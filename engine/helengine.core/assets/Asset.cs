namespace helengine {
    /// <summary>
    /// Base asset type containing a unique identifier.
    /// </summary>
    public class Asset {
        /// <summary>
        /// Gets or sets the asset identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the deterministic runtime asset identifier used by player caches.
        /// A value of zero indicates ephemeral runtime-only content.
        /// </summary>
        public ulong RuntimeAssetId { get; set; }

        /// <summary>
        /// Gets or sets the stable editor-authored UUID embedded in engine-native source files.
        /// Runtime-generated assets leave this value empty.
        /// </summary>
        public string AuthoringAssetId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets former editor-authored UUID aliases retained after duplicate identity repair.
        /// </summary>
        public string[] FormerAuthoringAssetIds { get; set; } = Array.Empty<string>();
    }
}
