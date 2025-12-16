namespace helengine {
    /// <summary>
    /// Describes a 2D drawable that renders text.
    /// </summary>
    public interface ITextDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// Gets or sets the font used to render the text.
        /// </summary>
        FontAsset Font { get; set; }
    }
}
