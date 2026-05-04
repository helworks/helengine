using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared text-wrapping helper used by renderer-backed text components.
    /// </summary>
    public sealed class TextLayoutUtilsTests {
        /// <summary>
        /// Ensures word-separated text wraps cleanly at the configured width.
        /// </summary>
        [Fact]
        public void WrapText_WhenTextContainsSpaces_WrapsAtWordBoundaries() {
            FontAsset font = CreateFont();

            string wrappedText = InvokeWrapText("ab cd ef", font, 20);

            Assert.Equal("ab\ncd\nef", wrappedText);
        }

        /// <summary>
        /// Ensures long runs without spaces still wrap instead of overflowing the line.
        /// </summary>
        [Fact]
        public void WrapText_WhenTextHasNoSpaces_HardWrapsAtCharacterBoundaries() {
            FontAsset font = CreateFont();

            string wrappedText = InvokeWrapText("abcd", font, 20);

            Assert.Equal("ab\ncd", wrappedText);
        }

        /// <summary>
        /// Creates one deterministic font asset for wrapping tests.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/.:\\-_ []()";

            for (int i = 0; i < glyphs.Length; i++) {
                characters[glyphs[i]] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
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

        /// <summary>
        /// Invokes the shared wrapping helper through reflection so the test can compile before the helper exists.
        /// </summary>
        /// <param name="text">Text to wrap.</param>
        /// <param name="font">Font metrics used for wrapping.</param>
        /// <param name="maxWidth">Maximum line width in pixels.</param>
        /// <returns>Wrapped text output.</returns>
        static string InvokeWrapText(string text, FontAsset font, int maxWidth) {
            Type textLayoutUtilsType = typeof(FontAsset).Assembly.GetType("helengine.TextLayoutUtils");
            Assert.NotNull(textLayoutUtilsType);

            MethodInfo wrapTextMethod = textLayoutUtilsType.GetMethod("WrapText", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(wrapTextMethod);

            object wrappedText = wrapTextMethod.Invoke(null, new object[] {
                text,
                font,
                maxWidth
            });
            return Assert.IsType<string>(wrappedText);
        }
    }
}
