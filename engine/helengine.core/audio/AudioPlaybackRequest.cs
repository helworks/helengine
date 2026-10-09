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

        /// <summary>
        /// Gets or sets how many seconds into the asset playback should begin (0 plays from the start). Only backends that
        /// implement <see cref="ISeekableAudioBackend"/> honour it; other backends (for example consoles that stream
        /// fixed sound banks) ignore it and always start at the beginning, so callers that need an exact position check
        /// <see cref="AudioManager.SupportsStartOffset"/> first.
        /// </summary>
        public float StartOffsetSeconds { get; set; }
    }
}
