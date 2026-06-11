using System.Drawing;
using System.Runtime.Versioning;

namespace helengine.editor.windows.tests.content.font {
    /// <summary>
    /// Verifies that GDI font atlas generation preserves grayscale edge coverage needed by low-resolution UI renderers.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class GDIFontProcessorTests {
        /// <summary>
        /// Ensures generated glyph atlases contain intermediate alpha coverage instead of only binary transparent or opaque texels.
        /// </summary>
        [Fact]
        public void ImportFont_WhenGeneratingAtlas_ProducesIntermediateAlphaCoverage() {
            using Font font = new Font(FontFamily.GenericSansSerif, 32f, FontStyle.Regular, GraphicsUnit.Pixel);

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font);

            Assert.NotNull(fontAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset.Colors);
            Assert.Contains(fontAsset.SourceTextureAsset.Colors.Where((value, index) => ((index + 1) % 4) == 0), alpha => alpha > 0 && alpha < byte.MaxValue);
        }

        /// <summary>
        /// Ensures the Nintendo DS debug-font pixel size still produces a recognizable capital H for the DS BG0 8x8 glyph upload path.
        /// </summary>
        [Fact]
        public void ImportFont_WhenUsingNintendoDsDebugFontPixelSize_ProducesRecognizableCapitalH() {
            using Font font = new Font("Consolas", 6f, FontStyle.Regular, GraphicsUnit.Pixel);

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font);

            Assert.NotNull(fontAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset.Colors);
            Assert.True(fontAsset.Characters.TryGetValue('H', out FontChar glyph));

            int sourceX = (int)Math.Round(glyph.SourceRect.X * fontAsset.AtlasWidth);
            int sourceY = (int)Math.Round(glyph.SourceRect.Y * fontAsset.AtlasHeight);
            int sourceWidth = (int)Math.Round(glyph.SourceRect.Z * fontAsset.AtlasWidth);
            int sourceHeight = (int)Math.Round(glyph.SourceRect.W * fontAsset.AtlasHeight);

            Assert.True(sourceWidth >= 3);
            Assert.True(sourceHeight >= 4);

            int widestOpaqueRowPixelCount = 0;
            int multiStemRowCount = 0;
            bool foundCrossbarRow = false;
            bool foundTwoStemRow = false;
            for (int y = 0; y < sourceHeight; y++) {
                int opaqueRowPixelCount = 0;
                int leftMostOpaqueColumn = int.MaxValue;
                int rightMostOpaqueColumn = int.MinValue;
                for (int x = 0; x < sourceWidth; x++) {
                    int pixelOffset = ((((sourceY + y) * fontAsset.SourceTextureAsset.Width) + (sourceX + x)) * 4) + 3;
                    byte alpha = fontAsset.SourceTextureAsset.Colors[pixelOffset];
                    if (alpha == 0) {
                        continue;
                    }

                    opaqueRowPixelCount++;
                    leftMostOpaqueColumn = Math.Min(leftMostOpaqueColumn, x);
                    rightMostOpaqueColumn = Math.Max(rightMostOpaqueColumn, x);
                }

                widestOpaqueRowPixelCount = Math.Max(widestOpaqueRowPixelCount, opaqueRowPixelCount);
                if (opaqueRowPixelCount >= 2) {
                    multiStemRowCount++;
                }
                if (opaqueRowPixelCount >= 3) {
                    foundCrossbarRow = true;
                }
                if (leftMostOpaqueColumn != int.MaxValue && rightMostOpaqueColumn - leftMostOpaqueColumn >= 2) {
                    foundTwoStemRow = true;
                }
            }

            Assert.True(foundTwoStemRow);
            Assert.True(multiStemRowCount >= 3);
            Assert.True(foundCrossbarRow);
            Assert.True(widestOpaqueRowPixelCount >= 3);
        }
    }
}
