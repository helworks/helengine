namespace helengine {
    /// <summary>
    /// Selects where one template element takes its fill color from.
    /// </summary>
    public enum GraphicElementColor {
        /// <summary>
        /// The text color of the overlay's text style.
        /// </summary>
        Text = 0,

        /// <summary>
        /// The highlight color of the overlay's text style.
        /// </summary>
        Highlight = 1,

        /// <summary>
        /// The color parameter named by <see cref="GraphicTemplateElementAsset.ColorParameter"/>, or its default.
        /// </summary>
        Parameter = 2,

        /// <summary>
        /// The color parameter named by <see cref="GraphicTemplateElementAsset.ColorParameter"/> when the edit sets it,
        /// otherwise the text style's highlight color, so accents follow the channel style unless overridden.
        /// </summary>
        Accent = 3
    }
}
