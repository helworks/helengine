namespace helengine.video {
    /// <summary>
    /// Chosen caption cue boundaries of one scene, replacing the automatic split. Each entry is the phrase that opens a
    /// new cue, matched in spoken order against the scene words, so the choice survives a recording replacing the planned
    /// speech.
    /// </summary>
    public sealed class VideoCaptionBreaks {
        /// <summary>
        /// Scene id.
        /// </summary>
        public string Scene { get; set; } = "";

        /// <summary>
        /// Opening words of every cue after the first, in order (e.g. "não de movimento").
        /// </summary>
        public List<string> Starts { get; set; } = [];

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
