namespace helengine.video {
    /// <summary>
    /// Zoom target of a motion preset.
    /// </summary>
    public sealed class VideoFocus {
        /// <summary>
        /// center, point or text_region.
        /// </summary>
        public string Type { get; set; } = "center";

        /// <summary>
        /// Normalized x for point.
        /// </summary>
        public double X { get; set; } = 0.5;

        /// <summary>
        /// Normalized y for point.
        /// </summary>
        public double Y { get; set; } = 0.5;

        /// <summary>
        /// Phrase for text_region, resolved into a point by the product before compilation.
        /// </summary>
        public string Text { get; set; }

    }
}
