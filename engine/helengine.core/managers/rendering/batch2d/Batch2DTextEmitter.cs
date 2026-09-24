namespace helengine {
    /// <summary>
    /// Expands shared text layout and font metrics into ordered glyph quads in a batch writer.
    /// </summary>
    public static class Batch2DTextEmitter {
        /// <summary>
        /// Lays out and appends text glyphs using the shared wrapping, alignment, metrics, and effect-pass helpers.
        /// </summary>
        /// <param name="writer">Writer that synchronously snapshots appended glyph vertices.</param>
        /// <param name="drawable">Text drawable whose current values are copied into the output vertices.</param>
        /// <param name="run">Textured run carrying the font atlas and resolved scissor snapshot.</param>
        public static void EmitText(Batch2DWriter writer, ITextDrawable2D drawable, Batch2DRun run) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (drawable == null || drawable.Parent == null || !drawable.Parent.Enabled) {
                return;
            }

            string text = drawable.Text ?? string.Empty;
            if (text.Length == 0) {
                return;
            }

            FontAsset font = drawable.Font;
            if (font == null) {
                throw new InvalidOperationException("Cannot emit nonempty text without a font asset.");
            }

            RuntimeTexture atlas = font.Texture;
            if (atlas == null) {
                throw new InvalidOperationException("Cannot emit nonempty text without a font atlas texture.");
            }
            if (atlas.IsDisposed) {
                throw new InvalidOperationException("Cannot emit nonempty text with a disposed font atlas texture.");
            }

            double fontScale = Math.Max((double)drawable.FontScale, 0.0001d);
            if (drawable.WrapText) {
                int maximumWidth = Math.Max(1, (int)Math.Round(drawable.Size.X / fontScale));
                text = TextLayoutUtils.WrapText(text, font, maximumWidth);
            }
            if (text.Length == 0) {
                return;
            }

            List<TextRenderEffectPass> effectPasses = null;
            double[] lineOffsets = null;
            try {
                effectPasses = TextRenderEffectPassBuilder.Build(drawable);
                lineOffsets = TextLineOffsets2D.Build(drawable, font, text, fontScale, atlas.Width);
                EmitLaidOutText(writer, drawable, font, atlas, text, fontScale, effectPasses, lineOffsets, run);
            } finally {
                NativeOwnership.Release(ref lineOffsets);
                NativeOwnership.Release(ref effectPasses);
            }
        }

        /// <summary>
        /// Traverses the wrapped string in glyph-major order and appends one quad for each effect pass.
        /// </summary>
        /// <param name="drawable">Drawable providing baseline, alignment, and per-glyph style values.</param>
        /// <param name="font">Font metrics and glyph dictionary.</param>
        /// <param name="atlas">Live atlas borrowed by the submitted text run.</param>
        /// <param name="text">Final text after optional wrapping.</param>
        /// <param name="fontScale">Resolved minimum-clamped glyph scale.</param>
        /// <param name="effectPasses">Ordered shadow, outline, and main passes.</param>
        /// <param name="lineOffsets">Offsets built from the final text, with one entry per newline-delimited line.</param>
        /// <param name="run">Scissor snapshot supplied for this text drawable.</param>
        static void EmitLaidOutText(Batch2DWriter writer, ITextDrawable2D drawable, FontAsset font, RuntimeTexture atlas, string text,
            double fontScale, List<TextRenderEffectPass> effectPasses, double[] lineOffsets, Batch2DRun run) {
            float3 position = drawable.Parent.Position;
            double lineHeight = Math.Max((double)font.LineHeight * fontScale, 1d);
            double baseX = Math.Round(position.X);
            double baseY = Math.Round(position.Y);
            double offsetX = 0d;
            double offsetY = 0d;
            int lineIndex = 0;
            // Build uses the final text's newline split; this traversal advances lineIndex only for those same newlines.
            double lineOriginX = baseX + lineOffsets[lineIndex];
            Batch2DRun textRun = new Batch2DRun(Batch2DVariant.Textured, atlas,
                run.ScissorX, run.ScissorY, run.ScissorWidth, run.ScissorHeight);

            for (int characterIndex = 0; characterIndex < text.Length; characterIndex++) {
                char character = text[characterIndex];
                if (character == '\n') {
                    offsetY += lineHeight;
                    offsetX = 0d;
                    lineIndex++;
                    lineOriginX = baseX + lineOffsets[lineIndex];
                    continue;
                }

                if (character == ' ') {
                    offsetX += font.FontInfo.SpaceWidth * fontScale;
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                double pixelWidth = glyph.SourceRect.Z * atlas.Width * fontScale;
                double pixelHeight = glyph.SourceRect.W * atlas.Height * fontScale;
                double snappedLineOffsetY = Math.Round(offsetY);
                double advance = glyph.AdvanceWidth > 0f ? glyph.AdvanceWidth * fontScale : pixelWidth;
                double glyphX = lineOriginX + offsetX;
                double glyphY = baseY + snappedLineOffsetY + glyph.OffsetY * fontScale;
                offsetX += advance;

                for (int passIndex = 0; passIndex < effectPasses.Count; passIndex++) {
                    TextRenderEffectPass pass = effectPasses[passIndex];
                    float4 color = Batch2DGeometry.NormalizeColor(pass.Color);
                    Batch2DGeometry.CreateQuad(glyphX + pass.Offset.X, glyphY + pass.Offset.Y,
                        pixelWidth, pixelHeight, 0d, 0d, glyph.SourceRect, color,
                        out Batch2DVertex topLeft, out Batch2DVertex topRight,
                        out Batch2DVertex bottomRight, out Batch2DVertex bottomLeft);
                    writer.AppendQuad(textRun, topLeft, topRight, bottomRight, bottomLeft);
                }
            }
        }
    }
}
