namespace helengine.video {
    /// <summary>
    /// Motion preset expanded into animations at compile time.
    /// </summary>
    public sealed class VideoMotion {
        /// <summary>
        /// none or zoom_to_focus.
        /// </summary>
        public string Preset { get; set; } = "none";

        /// <summary>
        /// Zoom at the start.
        /// </summary>
        public double FromScale { get; set; } = 1;

        /// <summary>
        /// Zoom at the end.
        /// </summary>
        public double ToScale { get; set; } = 1;

        /// <summary>
        /// When the motion starts; scene start when absent.
        /// </summary>
        public VideoMoment Start { get; set; }

        /// <summary>
        /// When the motion ends; scene end when absent.
        /// </summary>
        public VideoMoment End { get; set; }

        /// <summary>
        /// Catalog curve id.
        /// </summary>
        public string Curve { get; set; } = "smoothstep.v1";

        /// <summary>
        /// Zoom target.
        /// </summary>
        public VideoFocus Focus { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
