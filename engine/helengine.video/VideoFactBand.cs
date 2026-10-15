namespace helengine.video {
    /// <summary>
    /// A horizontal band of the output frame, as fractions of the frame height.
    /// </summary>
    public sealed class VideoFactBand {
        /// <summary>
        /// Top edge as a fraction of the frame height.
        /// </summary>
        public double Top { get; set; } = 0;

        /// <summary>
        /// Bottom edge as a fraction of the frame height.
        /// </summary>
        public double Bottom { get; set; } = 0;
    }
}
