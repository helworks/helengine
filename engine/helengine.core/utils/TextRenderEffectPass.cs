namespace helengine {
    /// <summary>
    /// Describes one ordered glyph draw pass used to render text effects or the primary text color.
    /// </summary>
    public sealed class TextRenderEffectPass {
        /// <summary>
        /// Initializes a text effect pass with a pixel-space offset and tint color.
        /// </summary>
        /// <param name="offset">Pixel-space offset applied to the glyph destination rectangle.</param>
        /// <param name="color">Color used for the glyph draw.</param>
        public TextRenderEffectPass(float2 offset, byte4 color) {
            Offset = offset;
            Color = color;
        }

        /// <summary>
        /// Gets the pixel-space offset applied to the glyph destination rectangle.
        /// </summary>
        public float2 Offset { get; }

        /// <summary>
        /// Gets the tint color used for this glyph draw pass.
        /// </summary>
        public byte4 Color { get; }
    }
}
