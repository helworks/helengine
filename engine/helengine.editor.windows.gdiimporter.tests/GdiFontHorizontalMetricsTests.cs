using helengine;
using helengine.editor;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Verifies native horizontal metrics are cooked once while the same importer retains exact legacy raster and vertical geometry.</summary>
    public sealed class GdiFontHorizontalMetricsTests {
        /// <summary>Real button, title and carousel font sizes retain all atlas bytes and glyph UVs while only native advances become integral.</summary>
        /// <param name="sourceName">Actual source face file.</param>
        /// <param name="pixelSize">Target native pixel size.</param>
        [Theory]
        [InlineData("trebuc.ttf", 40)]
        [InlineData("trebucbd.ttf", 34)]
        [InlineData("trebuc.ttf", 20)]
        public void ImportKeepsRasterAndSnapsOnlyHorizontalMetrics(string sourceName, int pixelSize) {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), sourceName);
            using FileStream legacyStream = File.OpenRead(path);
            using FileStream nativeStream = File.OpenRead(path);
            using FontAsset legacy = new GdiFontImporter(null).ImportFont(legacyStream, new FontAssetProcessorSettings { PixelSize = pixelSize });
            using FontAsset native = new GdiFontImporter(null).ImportFont(nativeStream, new FontAssetProcessorSettings { PixelSize = pixelSize, SnapHorizontalMetricsToPixels = true });
            Assert.Equal(legacy.SourceTextureAsset.Colors, native.SourceTextureAsset.Colors);
            Assert.Equal(legacy.AtlasWidth, native.AtlasWidth); Assert.Equal(legacy.AtlasHeight, native.AtlasHeight);
            Assert.Equal(legacy.LineHeight, native.LineHeight); Assert.Equal(legacy.FontInfo.LineSpacing, native.FontInfo.LineSpacing);
            Assert.Equal((float)Math.Round(legacy.FontInfo.SpaceWidth, MidpointRounding.AwayFromZero), native.FontInfo.SpaceWidth);
            Assert.NotEqual(Math.Truncate(legacy.Characters['E'].AdvanceWidth), legacy.Characters['E'].AdvanceWidth);
            foreach (char character in legacy.Characters.Keys) {
                FontChar before = legacy.Characters[character];
                FontChar after = native.Characters[character];
                Assert.Equal(before.SourceRect, after.SourceRect); Assert.Equal(before.OffsetY, after.OffsetY);
                Assert.Equal(before.BearingX, after.BearingX); Assert.Equal(before.BearingY, after.BearingY);
                Assert.Equal((float)Math.Round(before.AdvanceWidth, MidpointRounding.AwayFromZero), after.AdvanceWidth);
                Assert.Equal(Math.Truncate(after.AdvanceWidth), after.AdvanceWidth);
            }
            float advance = native.Characters['E'].AdvanceWidth;
            Assert.Equal(advance * 8f, native.MeasureString("EEEEEEEE").X);
            Assert.Equal(advance * 2f + native.FontInfo.SpaceWidth, native.MeasureString("E E").X);
        }

        /// <summary>The opt-in uses away-from-zero midpoint rounding and is idempotent without modifying source glyph rectangles or bearings.</summary>
        [Fact]
        public void HorizontalMetricPolicyPreservesLegacyAndRoundsMidpoints() {
            using FontAsset font = new FontAsset(new FontInfo("fixture", 10, 2.5f), null,
                new Dictionary<char, FontChar> { ['E'] = new FontChar(new float4(0.1f, 0.2f, 0.3f, 0.4f), 1.5f, 5.5f, 0.25f, -0.25f) }, 10.25f, 16, 16);
            FontChar before = font.Characters['E'];
            FontHorizontalMetricsProcessor.Apply(font, new FontAssetProcessorSettings());
            Assert.Equal(before, font.Characters['E']); Assert.Equal(2.5f, font.FontInfo.SpaceWidth);
            FontHorizontalMetricsProcessor.Apply(font, new FontAssetProcessorSettings { SnapHorizontalMetricsToPixels = true });
            Assert.Equal(6f, font.Characters['E'].AdvanceWidth); Assert.Equal(3f, font.FontInfo.SpaceWidth);
            Assert.Equal(before.SourceRect, font.Characters['E'].SourceRect); Assert.Equal(10.25f, font.LineHeight);
            FontHorizontalMetricsProcessor.Apply(font, new FontAssetProcessorSettings { SnapHorizontalMetricsToPixels = true });
            Assert.Equal(6f, font.Characters['E'].AdvanceWidth);
        }
    }
}
