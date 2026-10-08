namespace helengine.video {
    /// <summary>
    /// Global tracks that cross scene boundaries.
    /// </summary>
    public sealed class VideoTracks {
        /// <summary>
        /// Music and sound effects.
        /// </summary>
        public List<VideoAudioTrack> Audio { get; set; } = [];

        /// <summary>
        /// Captions derived from the speech of the takes.
        /// </summary>
        public VideoCaptionTrack Captions { get; set; }

    }
}
