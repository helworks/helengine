namespace helengine.video {
    /// <summary>
    /// Speech analysis of a media file, in source seconds.
    /// </summary>
    public sealed class VideoMediaAnalysis {
        /// <summary>
        /// How the words were timed, e.g. whisper_cpp_token_timestamps.
        /// </summary>
        public string Method { get; set; } = "";

        /// <summary>
        /// Spoken words in order.
        /// </summary>
        public List<VideoWord> Words { get; set; } = [];

        /// <summary>
        /// Detected silences.
        /// </summary>
        public List<VideoSilence> Silences { get; set; } = [];

        /// <summary>
        /// Optional source spans, each [start, end] in seconds, where the speaker's voice is heard; planner data only.
        /// </summary>
        public List<double[]> Voiced { get; set; }

        /// <summary>
        /// Optional source spans, each [start, end] in seconds, where the speaker looks away from the camera; planner data
        /// only.
        /// </summary>
        public List<double[]> LookAway { get; set; }

    }
}
