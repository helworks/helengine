namespace helengine {
    /// <summary>
    /// Describes the layout used for the demo-disc platform information overlay in authored menu scenes.
    /// </summary>
    public class MenuPlatformInfoDefinition {
        /// <summary>
        /// Initializes one platform-info overlay definition.
        /// </summary>
        /// <param name="topMargin">Distance in authored canvas pixels from the top edge.</param>
        /// <param name="rightMargin">Distance in authored canvas pixels from the right edge.</param>
        /// <param name="lineSpacing">Vertical spacing in authored canvas pixels between the two text lines.</param>
        public MenuPlatformInfoDefinition(int topMargin, int rightMargin, int lineSpacing) {
            if (topMargin < 0) {
                throw new ArgumentOutOfRangeException(nameof(topMargin), "Top margin must be zero or greater.");
            }
            if (rightMargin < 0) {
                throw new ArgumentOutOfRangeException(nameof(rightMargin), "Right margin must be zero or greater.");
            }
            if (lineSpacing < 0) {
                throw new ArgumentOutOfRangeException(nameof(lineSpacing), "Line spacing must be zero or greater.");
            }

            TopMargin = topMargin;
            RightMargin = rightMargin;
            LineSpacing = lineSpacing;
        }

        /// <summary>
        /// Gets the distance in authored canvas pixels from the top edge.
        /// </summary>
        public int TopMargin { get; }

        /// <summary>
        /// Gets the distance in authored canvas pixels from the right edge.
        /// </summary>
        public int RightMargin { get; }

        /// <summary>
        /// Gets the vertical spacing in authored canvas pixels between the platform name and version lines.
        /// </summary>
        public int LineSpacing { get; }
    }
}
