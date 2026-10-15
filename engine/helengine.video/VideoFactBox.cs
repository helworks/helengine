namespace helengine.video {
    /// <summary>
    /// A box on screen as fractions of the output frame (0..1, top-left origin).
    /// </summary>
    public sealed class VideoFactBox {
        /// <summary>
        /// Left edge as a fraction of the frame width.
        /// </summary>
        public double Left { get; set; } = 0;

        /// <summary>
        /// Top edge as a fraction of the frame height.
        /// </summary>
        public double Top { get; set; } = 0;

        /// <summary>
        /// Right edge as a fraction of the frame width.
        /// </summary>
        public double Right { get; set; } = 0;

        /// <summary>
        /// Bottom edge as a fraction of the frame height.
        /// </summary>
        public double Bottom { get; set; } = 0;
    }
}
