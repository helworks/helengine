namespace helengine.video {
    /// <summary>
    /// Normalized rectangle inside the frame.
    /// </summary>
    public sealed class VideoViewport {
        /// <summary>
        /// Left edge, 0 to 1.
        /// </summary>
        public double X { get; set; } = 0;

        /// <summary>
        /// Top edge, 0 to 1.
        /// </summary>
        public double Y { get; set; } = 0;

        /// <summary>
        /// Width, 0 to 1.
        /// </summary>
        public double Width { get; set; } = 1;

        /// <summary>
        /// Height, 0 to 1.
        /// </summary>
        public double Height { get; set; } = 1;

    }
}
