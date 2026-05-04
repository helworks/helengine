using System.Text;

namespace helengine {
    /// <summary>
    /// Provides shared text layout helpers used by text drawables and renderer backends.
    /// </summary>
    public static class TextLayoutUtils {
        /// <summary>
        /// Wraps a text string to the supplied width using the font's glyph advances.
        /// </summary>
        /// <param name="text">Text content to wrap.</param>
        /// <param name="font">Font metrics used to measure glyph widths.</param>
        /// <param name="maxWidth">Maximum allowed width in pixels for one rendered line.</param>
        /// <returns>Text with inserted line breaks when wrapping is needed.</returns>
        public static string WrapText(string text, FontAsset font, int maxWidth) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            if (string.IsNullOrEmpty(text) || maxWidth <= 0) {
                return text ?? string.Empty;
            }

            StringBuilder wrappedText = new StringBuilder(text.Length + 16);
            int lineStartIndex = 0;
            int lastWrapIndex = -1;
            double lineWidth = 0d;
            double widthAtLastWrap = 0d;
            int wrappedLength = 0;

            for (int index = 0; index < text.Length; index++) {
                char character = text[index];

                if (character == '\r') {
                    continue;
                }

                if (character == '\n') {
                    wrappedText.Append('\n');
                    wrappedLength++;
                    lineStartIndex = wrappedLength;
                    lastWrapIndex = -1;
                    widthAtLastWrap = 0d;
                    lineWidth = 0d;
                    continue;
                }

                double characterWidth = GetCharacterAdvanceWidth(font, character);
                if (lineWidth > 0d && lineWidth + characterWidth > maxWidth) {
                    if (lastWrapIndex >= lineStartIndex) {
                        int suffixStartIndex = lastWrapIndex + 1;
                        string currentText = wrappedText.ToString();
                        string suffix = currentText.Substring(suffixStartIndex);
                        wrappedText = new StringBuilder(currentText.Substring(0, lastWrapIndex));
                        wrappedText.Append('\n');
                        wrappedText.Append(suffix);
                        wrappedLength = lastWrapIndex + 1 + suffix.Length;
                        lineStartIndex = wrappedLength - suffix.Length;
                        lineWidth -= widthAtLastWrap;
                    } else {
                        wrappedText.Append('\n');
                        wrappedLength++;
                        lineStartIndex = wrappedLength;
                        lineWidth = 0d;
                    }

                    lastWrapIndex = -1;
                    widthAtLastWrap = 0d;

                    if (character == ' ') {
                        continue;
                    }

                    index--;
                    continue;
                }

                wrappedText.Append(character);
                wrappedLength++;
                lineWidth += characterWidth;

                if (character == ' ') {
                    lastWrapIndex = wrappedLength - 1;
                    widthAtLastWrap = lineWidth;
                }
            }

            return wrappedText.ToString();
        }

        /// <summary>
        /// Gets the rendered advance width for one character using the supplied font metrics.
        /// </summary>
        /// <param name="font">Font metrics used to resolve the character width.</param>
        /// <param name="character">Character to measure.</param>
        /// <returns>Advance width in pixels.</returns>
        static double GetCharacterAdvanceWidth(FontAsset font, char character) {
            if (character == ' ') {
                return font.FontInfo.SpaceWidth;
            }

            FontChar glyph;
            if (!font.Characters.TryGetValue(character, out glyph)) {
                return 0d;
            }

            double pixelWidth = glyph.SourceRect.Z * font.AtlasWidth;
            if (glyph.AdvanceWidth > 0f) {
                return glyph.AdvanceWidth;
            }

            return pixelWidth;
        }
    }
}
