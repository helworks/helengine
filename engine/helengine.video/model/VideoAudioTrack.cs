namespace helengine.video {
    /// <summary>
    /// One global audio clip such as music.
    /// </summary>
    public sealed class VideoAudioTrack {
        /// <summary>
        /// Track id.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Audio or video media id.
        /// </summary>
        public string Media { get; set; } = "";

        /// <summary>
        /// Where the clip starts.
        /// </summary>
        public VideoTrackMoment Start { get; set; } = new();

        /// <summary>
        /// Where the clip ends; end of the video or of the media when absent.
        /// </summary>
        public VideoTrackMoment End { get; set; }

        /// <summary>
        /// Source start in seconds.
        /// </summary>
        public double InSec { get; set; } = 0;

        /// <summary>
        /// Linear gain.
        /// </summary>
        public double Gain { get; set; } = 1;

        /// <summary>
        /// Silences the clip.
        /// </summary>
        public bool Muted { get; set; } = false;

        /// <summary>
        /// Gain envelopes; At is measured from the clip start.
        /// </summary>
        public List<VideoEnvelope> Envelopes { get; set; } = [];

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
