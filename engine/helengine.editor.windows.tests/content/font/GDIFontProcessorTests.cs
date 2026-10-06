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

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font, null);

            Assert.NotNull(fontAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.NotNull(fontAsset.SourceTextureAsset.Colors);
            Assert.Contains(fontAsset.SourceTextureAsset.Colors.Where((value, index) => ((index + 1) % 4) == 0), alpha => alpha > 0 && alpha < byte.MaxValue);
            Assert.False(fontAsset.Texture.UsesRgbFontCoverage);
            for (int index = 0; index < fontAsset.SourceTextureAsset.Colors.Length; index += 4) {
                Assert.Equal(byte.MaxValue, fontAsset.SourceTextureAsset.Colors[index]);
                Assert.Equal(byte.MaxValue, fontAsset.SourceTextureAsset.Colors[index + 1]);
                Assert.Equal(byte.MaxValue, fontAsset.SourceTextureAsset.Colors[index + 2]);
            }
        }

        /// <summary>
        /// Ensures ClearType retains independent RGB edge coverage, grayscale fallback alpha, and empty padding.
        /// </summary>
        [Fact]
        public void ImportFont_WhenClearTypeEnabled_PreservesRgbCoverageAndGrayscaleAlpha() {
            using Font font = new Font("Consolas", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
            FontAsset fontAsset = GDIFontProcessor.ImportFont(font, null, true);
            Assert.True(fontAsset.Texture.UsesRgbFontCoverage);
            byte[] colors = fontAsset.SourceTextureAsset.Colors;
            bool foundSubpixelEdge = false;
            bool foundEmptyPixel = false;
            for (int index = 0; index < colors.Length; index += 4) {
                byte r = colors[index];
                byte g = colors[index + 1];
                byte b = colors[index + 2];
                Assert.Equal((byte)((r + g + b + 1) / 3), colors[index + 3]);
                foundSubpixelEdge |= r != g || g != b;
                foundEmptyPixel |= r == 0 && g == 0 && b == 0;
            }
            Assert.True(foundSubpixelEdge);
            Assert.True(foundEmptyPixel);
            Assert.Contains(colors.Where((value, index) => index % 4 == 3), alpha => alpha > 0 && alpha < byte.MaxValue);
        }

        /// <summary>
        /// Ensures packing editor-size ClearType glyphs preserves the rasterizer's exact channel coverage without another resampling pass.
        /// </summary>
        [Theory]
        [InlineData('H', 12f, FontStyle.Regular)]
        [InlineData('m', 12f, FontStyle.Regular)]
        [InlineData('W', 15f, FontStyle.Bold)]
        public void ImportFont_WhenPackingClearTypeGlyph_PreservesRasterizedPixels(char character, float size, FontStyle style) {
            using Font font = new Font("Consolas", size, style, GraphicsUnit.Pixel);
            using Bitmap reference = new Bitmap(64, 64);
            using Graphics graphics = Graphics.FromImage(reference);
            int offset = (int)(Math.Max(16, Math.Ceiling(font.GetHeight(graphics))) * 0.1d);
            graphics.Clear(System.Drawing.Color.Black);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.TextContrast = 0;
            graphics.DrawString(character.ToString(), font, Brushes.White, new PointF(offset, offset), StringFormat.GenericTypographic);
            int left = 64;
            int top = 64;
            for (int y = 0; y < reference.Height; y++) {
                for (int x = 0; x < reference.Width; x++) {
                    System.Drawing.Color pixel = reference.GetPixel(x, y);
                    if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0) {
                        left = Math.Min(left, x);
                        top = Math.Min(top, y);
                    }
                }
            }
            FontAsset fontAsset = GDIFontProcessor.ImportFont(font, null, true);
            FontChar glyph = fontAsset.Characters[character];
            int atlasX = (int)Math.Round(glyph.SourceRect.X * fontAsset.AtlasWidth);
            int atlasY = (int)Math.Round(glyph.SourceRect.Y * fontAsset.AtlasHeight);
            int width = (int)Math.Round(glyph.SourceRect.Z * fontAsset.AtlasWidth);
            int height = (int)Math.Round(glyph.SourceRect.W * fontAsset.AtlasHeight);
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    System.Drawing.Color expected = reference.GetPixel(left - 1 + x, top - 1 + y);
                    int index = (((atlasY + y) * fontAsset.AtlasWidth) + atlasX + x) * 4;
                    Assert.Equal(expected.R, fontAsset.SourceTextureAsset.Colors[index]);
                    Assert.Equal(expected.G, fontAsset.SourceTextureAsset.Colors[index + 1]);
                    Assert.Equal(expected.B, fontAsset.SourceTextureAsset.Colors[index + 2]);
                }
            }
        }

        /// <summary>
        /// Ensures the Nintendo DS debug-font pixel size still produces a recognizable capital H for the DS BG0 8x8 glyph upload path.
        /// </summary>
        [Fact]
        public void ImportFont_WhenUsingNintendoDsDebugFontPixelSize_ProducesRecognizableCapitalH() {
            using Font font = new Font("Consolas", 6f, FontStyle.Regular, GraphicsUnit.Pixel);

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font, null);

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

        /// <summary>
        /// Ensures blank glyphs such as spaces can be imported into the generated DS debug font without crashing atlas generation.
        /// </summary>
        [Fact]
        public void ImportFont_WhenUsingNintendoDsDebugFontPixelSize_IncludesSpaceGlyphWithoutThrowing() {
            using Font font = new Font("Consolas", 8f, FontStyle.Regular, GraphicsUnit.Pixel);

            FontAsset fontAsset = GDIFontProcessor.ImportFont(font, null);

            Assert.NotNull(fontAsset);
            Assert.True(fontAsset.Characters.TryGetValue(' ', out FontChar glyph));
            Assert.True(glyph.AdvanceWidth > 0f);
            Assert.True(glyph.SourceRect.Z >= 0f);
            Assert.True(glyph.SourceRect.W >= 0f);
        }
    }
}
