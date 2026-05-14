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
    }
}
