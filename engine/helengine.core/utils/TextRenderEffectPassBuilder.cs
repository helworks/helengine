namespace helengine {
    /// <summary>
    /// Builds the deterministic shadow, outline, and main-glyph pass sequence for a text drawable.
    /// </summary>
    public static class TextRenderEffectPassBuilder {
        /// <summary>
        /// Creates the ordered glyph passes for one text drawable.
        /// </summary>
        /// <param name="drawable">Text drawable whose effect values determine the pass sequence.</param>
        /// <returns>Shadow, outline, and main-glyph passes in submission order.</returns>
        public static List<TextRenderEffectPass> Build(ITextDrawable2D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            List<TextRenderEffectPass> passes = new List<TextRenderEffectPass>(6);
            if (drawable.ShadowOffset.X != 0f || drawable.ShadowOffset.Y != 0f) {
                passes.Add(new TextRenderEffectPass(drawable.ShadowOffset, drawable.ShadowColor));
            }

            if (drawable.OutlineScale > 0f) {
                passes.Add(new TextRenderEffectPass(new float2(-drawable.OutlineScale, 0f), drawable.OutlineColor));
                passes.Add(new TextRenderEffectPass(new float2(drawable.OutlineScale, 0f), drawable.OutlineColor));
                passes.Add(new TextRenderEffectPass(new float2(0f, -drawable.OutlineScale), drawable.OutlineColor));
                passes.Add(new TextRenderEffectPass(new float2(0f, drawable.OutlineScale), drawable.OutlineColor));
            }

            passes.Add(new TextRenderEffectPass(new float2(0f, 0f), drawable.Color));
            return passes;
        }
    }
}
