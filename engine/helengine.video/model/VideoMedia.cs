namespace helengine.video {
    /// <summary>
    /// One file used by the edit, pinned by hash, with optional speech analysis.
    /// </summary>
    public sealed class VideoMedia {
        /// <summary>
        /// Media id referenced by takes, layers and tracks.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// One of video, audio, image or font.
        /// </summary>
        public string Kind { get; set; } = "video";

        /// <summary>
        /// Path relative to the render assets root.
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// SHA-256 of the file bytes.
        /// </summary>
        public string Sha256 { get; set; } = "";

        /// <summary>
        /// Pixel width for images and video.
        /// </summary>
        public int Width { get; set; } = 0;

        /// <summary>
        /// Pixel height for images and video.
        /// </summary>
        public int Height { get; set; } = 0;

        /// <summary>
        /// Duration in seconds for video and audio; zero for stills and fonts.
        /// </summary>
        public double DurationSec { get; set; } = 0;

        /// <summary>
        /// Whether a video or audio file carries a usable audio stream; takes only produce voice when true.
        /// </summary>
        public bool HasAudio { get; set; } = false;

        /// <summary>
        /// Optional speech analysis in source time.
        /// </summary>
        public VideoMediaAnalysis Analysis { get; set; }

    }
}
