namespace helengine {
    /// <summary>
    /// Identifies one resolved 2D command stored inside the transient command list for a camera frame.
    /// </summary>
    public enum RenderCommand2DType : byte {
        /// <summary>
        /// Draws one textured quad with a runtime texture, source rectangle, destination bounds, and tint color.
        /// </summary>
        TexturedQuad = 1,

        /// <summary>
        /// Draws one glyph quad sampled from a font atlas texture.
        /// </summary>
        GlyphQuad = 2,

        /// <summary>
        /// Draws one rounded rectangle while preserving radius, border, and corner-mask data.
        /// </summary>
        RoundedRect = 3
    }
}
