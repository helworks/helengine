namespace helengine.video {
    /// <summary>
    /// One keyframe at a scene moment.
    /// </summary>
    public sealed class VideoKeyframe {
        /// <summary>
        /// When the value is reached.
        /// </summary>
        public VideoMoment At { get; set; } = new();

        /// <summary>
        /// Property value.
        /// </summary>
        public double Value { get; set; } = 0;

        /// <summary>
        /// Catalog curve toward the next keyframe.
        /// </summary>
        public string Curve { get; set; } = "linear.v1";

    }
}
