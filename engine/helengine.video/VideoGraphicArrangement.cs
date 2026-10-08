namespace helengine.video {
    /// <summary>
    /// Result of laying out one graphic: the chosen direction, the uniform fit scale and the item font size it produced.
    /// Block positions are written onto the blocks themselves.
    /// </summary>
    public sealed class VideoGraphicArrangement {
        /// <summary>
        /// Gets the chosen stacking direction.
        /// </summary>
        public GraphicLayoutDirection Direction { get; }

        /// <summary>
        /// Gets the uniform scale applied to fit the safe area; one when the graphic fits at the style size.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Gets the item font size after fitting, in output pixels.
        /// </summary>
        public double FontSize { get; }

        /// <summary>
        /// Creates one arrangement.
        /// </summary>
        /// <param name="direction">Chosen direction.</param>
        /// <param name="scale">Fit scale.</param>
        /// <param name="fontSize">Item font size after fitting.</param>
        public VideoGraphicArrangement(GraphicLayoutDirection direction, double scale, double fontSize) {
            Direction = direction;
            Scale = scale;
            FontSize = fontSize;
        }
    }
}
