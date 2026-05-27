namespace helengine {
    /// <summary>
    /// Computes visible glyph widths and horizontal alignment offsets for text rendered inside one authored layout box.
    /// </summary>
    public static class TextLayoutAlignmentUtils {
        /// <summary>
        /// Measures the visible width of one rendered text line using glyph bounds instead of only advance widths.
        /// </summary>
        /// <param name="line">Single text line to measure.</param>
        /// <param name="font">Font asset that supplies glyph metrics.</param>
        /// <param name="fontScale">Uniform scale applied to glyph metrics.</param>
        /// <param name="textureWidth">Atlas texture width used to resolve normalized glyph widths into pixels.</param>
        /// <returns>Visible line width in pixels.</returns>
        public static double MeasureVisibleLineWidth(string line, FontAsset font, double fontScale, double textureWidth) {
            if (line == null) {
                throw new ArgumentNullException(nameof(line));
            } else if (font == null) {
                throw new ArgumentNullException(nameof(font));
            } else if (fontScale <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(fontScale), "Font scale must be greater than zero.");
            } else if (textureWidth <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(textureWidth), "Texture width must be greater than zero.");
            }

            double visibleWidth = 0d;
            double offsetX = 0d;
            double spaceWidth = font.FontInfo != null
                ? Math.Max(font.FontInfo.SpaceWidth * fontScale, 1d)
                : Math.Max(font.LineHeight * fontScale * 0.25d, 1d);

            for (int index = 0; index < line.Length; index++) {
                char character = line[index];
                if (character == ' ') {
                    offsetX += spaceWidth;
                    visibleWidth = Math.Max(visibleWidth, offsetX);
                    continue;
                }

                if (!font.Characters.TryGetValue(character, out FontChar glyph)) {
                    continue;
                }

                double glyphWidth = Math.Max(1d, glyph.SourceRect.Z * textureWidth * fontScale);
                visibleWidth = Math.Max(visibleWidth, offsetX + glyphWidth);
                offsetX += glyph.AdvanceWidth > 0f
                    ? glyph.AdvanceWidth * fontScale
                    : glyphWidth;
            }

            return visibleWidth;
        }

        /// <summary>
        /// Resolves the horizontal offset required to place one line according to its authored text alignment.
        /// </summary>
        /// <param name="alignment">Horizontal alignment requested by the text component.</param>
        /// <param name="boxWidth">Authored layout width available to the line.</param>
        /// <param name="visibleWidth">Measured visible glyph width for the line.</param>
        /// <returns>Horizontal offset that should be added before drawing the line.</returns>
        public static double ResolveHorizontalOffset(TextAlignment alignment, int boxWidth, double visibleWidth) {
            if (boxWidth <= 0 || visibleWidth <= 0d) {
                return 0d;
            }

            double availableWidth = boxWidth - visibleWidth;
            if (availableWidth <= 0d) {
                return 0d;
            }

            if (alignment == TextAlignment.Center) {
                return availableWidth * 0.5d;
            } else if (alignment == TextAlignment.Right) {
                return availableWidth;
            }

            return 0d;
        }
    }
}
