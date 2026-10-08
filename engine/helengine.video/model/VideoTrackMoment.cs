namespace helengine.video {
    /// <summary>
    /// A point in global time expressed as a moment of a scene.
    /// </summary>
    public sealed class VideoTrackMoment {
        /// <summary>
        /// Scene id.
        /// </summary>
        public string Scene { get; set; } = "";

        /// <summary>
        /// Moment inside that scene.
        /// </summary>
        public VideoMoment At { get; set; } = new();

    }
}
