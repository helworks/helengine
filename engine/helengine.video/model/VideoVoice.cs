namespace helengine.video {
    /// <summary>
    /// Adjustments to the take audio of a scene.
    /// </summary>
    public sealed class VideoVoice {
        /// <summary>
        /// Linear gain.
        /// </summary>
        public double Gain { get; set; } = 1;

        /// <summary>
        /// Silences the take audio.
        /// </summary>
        public bool Muted { get; set; } = false;

        /// <summary>
        /// Gain envelopes in scene time.
        /// </summary>
        public List<VideoEnvelope> Envelopes { get; set; } = [];

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
