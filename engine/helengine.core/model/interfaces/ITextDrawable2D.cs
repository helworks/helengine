namespace helengine {
    /// <summary>
    /// Describes a 2D drawable that renders text.
    /// </summary>
    public interface ITextDrawable2D : IDrawable2D {
        /// <summary>
        /// Gets or sets the tint color applied to the text.
        /// </summary>
        byte4 Color { get; set; }

        /// <summary>
        /// Gets or sets the source rectangle into the font atlas.
        /// </summary>
        float4 SourceRect { get; set; }

        /// <summary>
        /// Gets or sets the layout size for the rendered text.
        /// </summary>
        int2 Size { get; set; }

        /// <summary>
        /// Gets or sets the text content.
        /// </summary>
        string Text { get; set; }

        /// <summary>
        /// Gets or sets whether text should wrap within the drawable's layout width.
        /// </summary>
        bool WrapText { get; set; }

        /// <summary>
        /// Gets or sets the font used to render the text.
        /// </summary>
        FontAsset Font { get; set; }

        /// <summary>
        /// Gets or sets the uniform scale applied to glyph bounds, advances, and line height during rendering.
        /// </summary>
        float FontScale { get; set; }

        /// <summary>
        /// Gets or sets how glyphs are positioned horizontally inside the authored text layout box.
        /// </summary>
        TextAlignment Alignment { get; set; }
    }
}
