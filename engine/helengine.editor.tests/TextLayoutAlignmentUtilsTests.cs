using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies visible-width measurement and horizontal-alignment helpers for authored text layout boxes.
    /// </summary>
    public sealed class TextLayoutAlignmentUtilsTests {
        /// <summary>
        /// Ensures empty lines report zero width so wrapped and blank lines can be rendered without native crashes.
        /// </summary>
        [Fact]
        public void MeasureVisibleLineWidth_WhenLineIsEmpty_ReturnsZero() {
            FontAsset font = CreateFont();

            double visibleWidth = TextLayoutAlignmentUtils.MeasureVisibleLineWidth(string.Empty, font, 1d, 128d);

            Assert.Equal(0d, visibleWidth);
        }

        /// <summary>
        /// Creates one deterministic font asset for text-layout width tests.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics and a predictable atlas size.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/.:\\-_ []()";

            for (int index = 0; index < glyphs.Length; index++) {
                characters[glyphs[index]] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 128,
                    Height = 128
                },
                characters,
                16f,
                128,
                128);
        }
    }
}
