namespace helengine {
    /// <summary>
    /// Describes one runtime playback request issued through the shared audio manager.
    /// </summary>
    public sealed class AudioPlaybackRequest {
        /// <summary>
        /// Gets or sets the target mixer bus identifier.
        /// </summary>
        public string BusId { get; set; } = "master";

        /// <summary>
        /// Gets or sets whether playback should loop.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets or sets the linear gain multiplier applied to playback.
        /// </summary>
        public float Gain { get; set; } = 1f;
    }
}
