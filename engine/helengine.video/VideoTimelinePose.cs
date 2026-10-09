namespace helengine.video {
    /// <summary>
    /// The state of one overlay timeline slot at one instant, in timeline units: position in box units (x right, y down,
    /// -0.5..0.5 spans the box), rotation in degrees clockwise, scale factors, opacity (0 while the slot is inactive) and
    /// reveal (rect slots).
    /// </summary>
    public sealed class VideoTimelinePose {
        /// <summary>
        /// Horizontal position of the element center, in box widths from the box center.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Vertical position of the element center, in box heights from the box center (down is positive).
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Clockwise rotation in degrees.
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>
        /// Horizontal scale factor.
        /// </summary>
        public double ScaleX { get; set; } = 1;

        /// <summary>
        /// Vertical scale factor.
        /// </summary>
        public double ScaleY { get; set; } = 1;

        /// <summary>
        /// Opacity in 0..1, already multiplied by the activation state.
        /// </summary>
        public double Opacity { get; set; } = 1;

        /// <summary>
        /// Revealed fraction of a rectangle, growing from its left edge.
        /// </summary>
        public double Reveal { get; set; } = 1;
    }
}
