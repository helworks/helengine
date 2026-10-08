namespace helengine.video {
    /// <summary>
    /// Replaces the text of one caption cue.
    /// </summary>
    public sealed class VideoCaptionOverride {
        /// <summary>
        /// Scene id.
        /// </summary>
        public string Scene { get; set; } = "";

        /// <summary>
        /// Zero-based cue index within the scene.
        /// </summary>
        public int Cue { get; set; } = 0;

        /// <summary>
        /// Replacement text.
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
