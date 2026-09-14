namespace helengine {
    /// <summary>
    /// Computes the per-line horizontal offsets a 2D backend needs to honour authored text alignment, so every backend measures wrapped and non-wrapped lines the same way.
    /// </summary>
    public static class TextLineOffsets2D {
        /// <summary>
        /// Builds one horizontal offset per rendered text line so authored text alignment is respected consistently across wrapped and non-wrapped content.
        /// </summary>
        /// <param name="drawable">Text drawable that owns the authored layout box.</param>
        /// <param name="font">Font used to render the text.</param>
        /// <param name="content">Final rendered text content after wrapping has been applied.</param>
        /// <param name="fontScale">Resolved glyph scale.</param>
        /// <param name="textureWidth">Font-atlas texture width used to resolve glyph bounds.</param>
        /// <returns>One horizontal offset per rendered line.</returns>
        public static double[] Build(ITextDrawable2D drawable, FontAsset font, string content, double fontScale, int textureWidth) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            } else if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (content == null) {
                throw new ArgumentNullException(nameof(content));
            } else if (fontScale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(fontScale), "Font scale must be greater than zero.");
            } else if (textureWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(textureWidth), "Texture width must be greater than zero.");
            }

            string[] lines = content.Split('\n');
            double[] lineOffsets = new double[lines.Length];
            for (int index = 0; index < lines.Length; index++) {
                double visibleWidth = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(lines[index], font, fontScale, textureWidth);
                lineOffsets[index] = TextLayoutAlignmentUtils.ResolveHorizontalOffset(drawable.Alignment, drawable.Size.X, visibleWidth);
            }

            return lineOffsets;
        }

        /// <summary>
        /// Resolves one previously measured line offset or returns zero when the requested line index is outside the rendered line array.
        /// </summary>
        /// <param name="lineOffsets">Per-line horizontal offsets computed for the rendered text.</param>
        /// <param name="lineIndex">Rendered line index whose offset should be returned.</param>
        /// <returns>Horizontal line offset in pixels.</returns>
        public static double Resolve(IReadOnlyList<double> lineOffsets, int lineIndex) {
            if (lineOffsets == null) {
                throw new ArgumentNullException(nameof(lineOffsets));
            }

            if (lineIndex < 0 || lineIndex >= lineOffsets.Count) {
                return 0d;
            }

            return lineOffsets[lineIndex];
        }
    }
}
