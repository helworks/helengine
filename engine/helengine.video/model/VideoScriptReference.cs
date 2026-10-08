namespace helengine.video {
    /// <summary>
    /// Provenance of the script an edit was planned from.
    /// </summary>
    public sealed class VideoScriptReference {
        /// <summary>
        /// Script id.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Script revision.
        /// </summary>
        public long Revision { get; set; } = 0;

        /// <summary>
        /// Optional hash of the script bytes.
        /// </summary>
        public string Sha256 { get; set; }

    }
}
