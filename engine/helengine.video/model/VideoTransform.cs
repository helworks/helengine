namespace helengine.video {
    /// <summary>
    /// Static layer transform.
    /// </summary>
    public sealed class VideoTransform {
        /// <summary>
        /// Horizontal offset in frame widths.
        /// </summary>
        public double PositionX { get; set; } = 0;

        /// <summary>
        /// Vertical offset in frame heights.
        /// </summary>
        public double PositionY { get; set; } = 0;

        /// <summary>
        /// Horizontal scale.
        /// </summary>
        public double ScaleX { get; set; } = 1;

        /// <summary>
        /// Vertical scale.
        /// </summary>
        public double ScaleY { get; set; } = 1;

        /// <summary>
        /// Rotation in degrees.
        /// </summary>
        public double RotationDeg { get; set; } = 0;

        /// <summary>
        /// Opacity, 0 to 1.
        /// </summary>
        public double Opacity { get; set; } = 1;

    }
}
