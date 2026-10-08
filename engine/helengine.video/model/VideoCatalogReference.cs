namespace helengine.video {
    /// <summary>
    /// Pin of the engine capability catalog effects were chosen from.
    /// </summary>
    public sealed class VideoCatalogReference {
        /// <summary>
        /// Catalog schema id.
        /// </summary>
        public string Schema { get; set; } = "helengine.media.capabilities.v1";

        /// <summary>
        /// SHA-256 fingerprint of the catalog document.
        /// </summary>
        public string Fingerprint { get; set; } = "";

    }
}
