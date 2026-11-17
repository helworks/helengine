using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Protects dynamic window titles from fatal failures when a font lacks requested Unicode glyphs.</summary>
    public sealed class TextMeshMissingGlyphTests {
        /// <summary>Uses one replacement for each unavailable scalar, including a complete UTF-16 surrogate pair.</summary>
        [Theory]
        [InlineData(0x044d)]
        [InlineData(0x1f600)]
        [InlineData(0xd83d)]
        [InlineData(0xde00)]
        public void MissingCharactersUseReplacement(int codePoint) {
            string text = codePoint > 0xffff ? char.ConvertFromUtf32(codePoint) : ((char)codePoint).ToString();
            using FontAsset font = TextMeshGeometryTests.CreateFont();
            font.Characters['\ufffd'] = font.Characters['A'];
            font.Characters['?'] = font.Characters['\u00f3'];
            using ModelAsset expected = TextMeshGeometry.Build(font, "A");
            using ModelAsset actual = TextMeshGeometry.Build(font, text);
            Assert.Equal(expected.Positions, actual.Positions);
            Assert.Equal(expected.TexCoords, actual.TexCoords);
            Assert.Equal(new[] { 0 }, TextMeshGeometry.GetPageIndices(font, text));
        }
        /// <summary>Pins the replacement glyph's actual atlas page and preserves adjacent material runs.</summary>
        [Fact]
        public void QuestionMarkFallbackUsesItsOwnPage() {
            using FontAsset font = TextMeshGeometryTests.CreateFont();
            font.Pages = new[] { new FontPage("fallback.hasset", 128, 64) };
            FontChar fallback = font.Characters['A']; fallback.PageIndex = 1;
            font.Characters['?'] = fallback;
            using ModelAsset expected = TextMeshGeometry.Build(font, "A?A");
            using ModelAsset actual = TextMeshGeometry.Build(font, "A\u044dA");
            Assert.Equal(expected.Positions, actual.Positions);
            Assert.Equal(new[] { 0, 1, 0 }, TextMeshGeometry.GetPageIndices(font, "A\u044dA"));
            Assert.Equal(new[] { 6, 6, 6 }, actual.Submeshes.Select(slot => slot.IndexCount));
        }
        /// <summary>Reserves one space per unavailable scalar if neither replacement glyph is present.</summary>
        [Theory]
        [InlineData("\u044dA")]
        [InlineData("\ud83d\ude00A")]
        public void FontWithoutFallbackPreservesSpacing(string text) {
            using FontAsset font = TextMeshGeometryTests.CreateFont();
            using ModelAsset expected = TextMeshGeometry.Build(font, " A");
            using ModelAsset actual = TextMeshGeometry.Build(font, text);
            Assert.Equal(expected.Positions, actual.Positions);
            Assert.Equal(expected.TexCoords, actual.TexCoords);
            using ModelAsset empty = TextMeshGeometry.Build(font, "\u044d");
            Assert.Empty(empty.Positions);
            Assert.Empty(TextMeshGeometry.GetPageIndices(font, "\u044d"));
        }
        /// <summary>Missing input is recoverable; corrupt replacement metrics must still be rejected.</summary>
        [Fact]
        public void CorruptFallbackMetricsStillFail() {
            using FontAsset font = TextMeshGeometryTests.CreateFont();
            FontChar fallback = font.Characters['A']; fallback.BearingX = float.NaN;
            font.Characters['\ufffd'] = fallback;
            Assert.Throws<InvalidOperationException>(() => TextMeshGeometry.Build(font, "\u044d"));
        }
    }
}
