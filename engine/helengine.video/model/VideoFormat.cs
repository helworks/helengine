namespace helengine.video {
    /// <summary>
    /// Output frame size, frame rate and background of an edit.
    /// </summary>
    public sealed class VideoFormat {
        /// <summary>
        /// Output width in pixels.
        /// </summary>
        public int Width { get; set; } = 1080;

        /// <summary>
        /// Output height in pixels.
        /// </summary>
        public int Height { get; set; } = 1920;

        /// <summary>
        /// Output frame rate as a rational.
        /// </summary>
        public VideoRational FrameRate { get; set; } = new() { Numerator = 24, Denominator = 1 };

        /// <summary>
        /// Background color as #RRGGBBAA.
        /// </summary>
        public string BackgroundColor { get; set; } = "#000000FF";

    }
}
