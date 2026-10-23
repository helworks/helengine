using helengine;
using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Verifies exact glyph requests through the actual CPU rasterizer without a renderer or visible windows.</summary>
    public sealed class FontCharacterRequestTests {
        /// <summary>Default coverage is ASCII and explicit accent requests add only their selected cells.</summary>
        [Fact]
        public void DefaultAndExplicitCoverageAreExact() {
            using FontAsset basic = Import(new FontAssetProcessorSettings { PixelSize = 20 });
            Assert.Equal(Enumerable.Range(32, 95).Select(value => (char)value), basic.Characters.Keys.OrderBy(value => value));
            using FontAsset accented = Import(new FontAssetProcessorSettings { PixelSize = 20, Characters = FontCharacterSet.FromText("A\u00e3\u00e7A") });
            Assert.Equal(new[] { 'A', '\u00e3', '\u00e7' }, accented.Characters.Keys.OrderBy(value => value));
            Assert.All(accented.Characters.Values, glyph => Assert.True(glyph.AdvanceWidth > 0));
        }
        /// <summary>Digits-only requests work without an unrequested space cell, while retaining independently measured spacing.</summary>
        [Fact]
        public void DigitsOnlyHaveTenCellsAndIndependentSpaceMetrics() {
            using FontAsset digits = Import(new FontAssetProcessorSettings { PixelSize = 20, Characters = FontCharacterSet.FromText("01234567899876") });
            Assert.Equal("0123456789".ToCharArray(), digits.Characters.Keys.OrderBy(value => value));
            Assert.True(digits.FontInfo.SpaceWidth > 0);
            Assert.Contains(digits.SourceTextureAsset.Colors.Where((value, index) => index % 4 == 3), value => value > 0);
        }
        /// <summary>Unsupported requests fail before source parsing, temporary files or graphics uploads.</summary>
        [Fact]
        public void UnsupportedAndEmptySelectionsFailBeforeSourceParsing() {
            using MemoryStream empty = new();
            IFontImporter importer = new GdiFontImporter(null);
            Assert.Throws<NotSupportedException>(() => importer.ImportFont(empty, new FontAssetProcessorSettings { Characters = FontCharacterSet.FromText("\U0001f600") }));
            Assert.Throws<InvalidOperationException>(() => importer.ImportFont(empty, new FontAssetProcessorSettings { Characters = FontCharacterSet.FromText("") }));
        }
        /// <summary>The existing runtime font serializer preserves exactly the selected cells, metrics and raw atlas pixels without importing a platform font at runtime.</summary>
        [Fact]
        public void SelectedAtlasRoundTripsThroughExistingRuntimeFormat() {
            using FontAsset font = Import(new FontAssetProcessorSettings { PixelSize = 20, Characters = FontCharacterSet.FromText("019") });
            using MemoryStream output = new();
            helengine.files.FontAssetBinarySerializer.Serialize(output, font);
            using MemoryStream input = new(output.ToArray());
            using FontAsset restored = helengine.files.FontAssetBinarySerializer.Deserialize(input);
            Assert.Equal(input.Length, input.Position);
            Assert.Equal(font.Characters.Keys.OrderBy(value => value), restored.Characters.Keys.OrderBy(value => value));
            Assert.Equal(font.SourceTextureAsset.Colors, restored.SourceTextureAsset.Colors);
            Assert.Equal(font.FontInfo.SpaceWidth, restored.FontInfo.SpaceWidth);
            Assert.Equal(font.LineHeight, restored.LineHeight);
            Assert.Equal(font.AtlasWidth, restored.AtlasWidth);
            Assert.Equal(font.AtlasHeight, restored.AtlasHeight);
        }
        /// <summary>Imports a real source face with the supplied generation request and no system-family fallback.</summary>
        /// <param name="settings">Exact glyph and metric request.</param>
        /// <returns>The CPU-owned font atlas and metrics.</returns>
        static FontAsset Import(FontAssetProcessorSettings settings) {
            using FileStream source = File.OpenRead(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"));
            return new GdiFontImporter(null).ImportFont(source, settings);
        }
    }
}