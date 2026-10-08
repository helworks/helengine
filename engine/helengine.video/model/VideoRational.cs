namespace helengine.video {
    /// <summary>
    /// A rational number such as a frame rate.
    /// </summary>
    public sealed class VideoRational {
        /// <summary>
        /// Numerator.
        /// </summary>
        public long Numerator { get; set; } = 0;

        /// <summary>
        /// Denominator; always positive.
        /// </summary>
        public long Denominator { get; set; } = 1;

    }
}
