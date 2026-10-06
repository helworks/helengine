namespace helengine.editor {
    /// <summary>Measures the pixels actually drawn by text so world previews retain overflow beyond the authored layout box.</summary>
    public static class EditorTextPreviewBoundsService {
        /// <summary>Includes scaled glyphs, line alignment, wrapping, outlines, and shadows in the local capture rectangle.</summary>
        /// <param name="component">Text whose original layout box remains authoritative for alignment and wrapping.</param>
        /// <returns>Integer local origin and dimensions covering both the layout box and every rendered glyph pass.</returns>
        public static float4 ResolveBounds(TextComponent component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            double left = 0, top = 0;
            double right = Math.Max(1, component.Size.X), bottom = Math.Max(1, component.Size.Y);
            FontAsset font = component.Font;
            if (font == null || string.IsNullOrEmpty(component.Text)) {
                return new float4(0, 0, (float)right, (float)bottom);
            }
            double scale = Math.Max((double)component.FontScale, 0.0001);
            string text = component.WrapText
                ? TextLayoutUtils.WrapText(component.Text, font, Math.Max(1, (int)Math.Round(component.Size.X / scale)))
                : component.Text;
            double[] lineOffsets = TextLineOffsets2D.Build(component, font, text, scale, font.AtlasWidth);
            List<TextRenderEffectPass> passes = TextRenderEffectPassBuilder.Build(component);
            double x = 0, y = 0;
            int lineIndex = 0;
            foreach (char character in text) {
                if (character == '\n') {
                    y += Math.Max(font.LineHeight * scale, 1.0);
                    x = 0;
                    lineIndex++;
                    continue;
                }
                if (character == ' ') {
                    x += font.FontInfo.SpaceWidth * scale;
                    continue;
                }
                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }
                double width = glyph.SourceRect.Z * font.AtlasWidth * scale;
                double height = glyph.SourceRect.W * font.AtlasHeight * scale;
                foreach (TextRenderEffectPass pass in passes) {
                    double glyphX = TextLineOffsets2D.Resolve(lineOffsets, lineIndex) + x + pass.Offset.X;
                    double glyphY = Math.Round(y) + glyph.OffsetY * scale + pass.Offset.Y;
                    left = Math.Min(left, glyphX);
                    top = Math.Min(top, glyphY);
                    right = Math.Max(right, glyphX + width);
                    bottom = Math.Max(bottom, glyphY + height);
                }
                x += glyph.AdvanceWidth > 0 ? glyph.AdvanceWidth * scale : width;
            }
            left = Math.Floor(left);
            top = Math.Floor(top);
            return new float4((float)left, (float)top, (float)(Math.Ceiling(right) - left), (float)(Math.Ceiling(bottom) - top));
        }
    }
}
