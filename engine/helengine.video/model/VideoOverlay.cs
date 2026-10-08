namespace helengine.video {
    /// <summary>
    /// Timed text drawn over a scene.
    /// </summary>
    public sealed class VideoOverlay {
        /// <summary>
        /// Overlay id, unique within its scene.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Overlay text.
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// Text style id: graphic or caption.
        /// </summary>
        public string Style { get; set; } = "graphic";

        /// <summary>
        /// When the overlay appears.
        /// </summary>
        public VideoMoment At { get; set; } = new();

        /// <summary>
        /// When it disappears; scene end when absent.
        /// </summary>
        public VideoMoment Until { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
