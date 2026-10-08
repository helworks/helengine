namespace helengine.video {
    /// <summary>
    /// Timed text drawn over a scene, either as plain styled text or expanded from a graphic template.
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
        /// Name of the text style in the edit text styles.
        /// </summary>
        public string Style { get; set; } = "graphic";

        /// <summary>
        /// When the overlay appears; with a graphic it may be left empty, and the graphic then starts with its first item.
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

        /// <summary>
        /// Optional kinetic typography replacing the plain text rendering; <see cref="Text"/> stays as the fallback text.
        /// </summary>
        public VideoGraphic Graphic { get; set; }

    }
}
