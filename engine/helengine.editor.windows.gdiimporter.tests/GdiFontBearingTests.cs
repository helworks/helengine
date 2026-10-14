using helengine;
using helengine.editor;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
using Font = System.Drawing.Font;
using Rectangle = System.Drawing.Rectangle;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>Checks cropped atlas origins against independent GDI rasterization without changing glyph pixels or pen advances.</summary>
    public sealed class GdiFontBearingTests {
        /// <summary>The real forty-pixel button face reconstructs all glyphs at their original pen positions, including positive and negative overhangs.</summary>
        [Fact]
        public void ImportPreservesRasterCropBearingsAndPixels() {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "trebuc.ttf");
            using FileStream source = File.OpenRead(path);
            using FontAsset imported = new GdiFontImporter(null).ImportFont(source, new FontAssetProcessorSettings { PixelSize = 40 });
            using System.Drawing.Text.PrivateFontCollection collection = new System.Drawing.Text.PrivateFontCollection();
            collection.AddFontFile(path);
            using Font font = new Font(collection.Families[0], 40, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            using Bitmap measureBitmap = new Bitmap(1, 1);
            using Graphics measure = Graphics.FromImage(measureBitmap);
            using System.Drawing.StringFormat format = new System.Drawing.StringFormat(System.Drawing.StringFormat.GenericTypographic);
            format.FormatFlags |= System.Drawing.StringFormatFlags.MeasureTrailingSpaces;
            int resolution = Math.Max(16, (int)Math.Ceiling(font.GetHeight(measure)));
            int offset = (int)(resolution * 0.1f);
            Assert.Equal(112, imported.Characters.Count);
            Assert.Equal(512, imported.AtlasWidth);
            Assert.Equal(256, imported.AtlasHeight);
            Assert.Equal(2f, imported.Characters['B'].BearingX);
            Assert.Equal(0f, imported.Characters['e'].BearingX);
            Assert.Contains(imported.Characters.Values, glyph => glyph.BearingX > 0f);
            Assert.Contains(imported.Characters.Values, glyph => glyph.BearingX < 0f);
            Assert.Equal(font.GetHeight(measure), imported.LineHeight);
            foreach (char character in imported.Characters.Keys) {
                using Bitmap bitmap = Rasterize(character, font, resolution * 2, offset);
                Rectangle crop = FindCrop(bitmap, offset);
                FontChar glyph = imported.Characters[character];
                Assert.Equal((float)(crop.X - offset), glyph.BearingX);
                Assert.Equal((float)(crop.Y - offset), glyph.OffsetY);
                Assert.Equal(0f, glyph.BearingY);
                Assert.Equal((float)crop.Width, glyph.SourceRect.Z * imported.AtlasWidth);
                Assert.Equal((float)crop.Height, glyph.SourceRect.W * imported.AtlasHeight);
                Assert.Equal(measure.MeasureString(character.ToString(), font, System.Drawing.PointF.Empty, format).Width, glyph.AdvanceWidth);
                int atlasX = (int)(glyph.SourceRect.X * imported.AtlasWidth);
                int atlasY = (int)(glyph.SourceRect.Y * imported.AtlasHeight);
                for (int y = 0; y < crop.Height; y++) {
                    for (int x = 0; x < crop.Width; x++) {
                        byte expectedAlpha = bitmap.GetPixel(crop.X + x, crop.Y + y).A;
                        int atlasByte = ((atlasY + y) * imported.AtlasWidth + atlasX + x) * 4;
                        Assert.Equal(expectedAlpha, imported.SourceTextureAsset.Colors[atlasByte + 3]);
                        // The atlas local origin plus BearingX reconstructs the original raster-pen coordinate.
                        Assert.Equal(crop.X + x - offset, (int)glyph.BearingX + x);
                    }
                }
            }
        }

        /// <summary>Renders one isolated glyph using the exact public GDI settings of the maintained importer.</summary>
        /// <param name="character">Glyph whose original pen-relative pixels are measured.</param>
        /// <param name="font">Privately loaded source face in pixel units.</param>
        /// <param name="resolution">Temporary square bitmap side.</param>
        /// <param name="offset">Integer pen origin shared by both rasterizers.</param>
        /// <returns>Caller-owned bitmap containing the untouched GDI raster.</returns>
        static Bitmap Rasterize(char character, Font font, int resolution, int offset) {
            Bitmap bitmap = new Bitmap(resolution, resolution);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.DrawString(character.ToString(), font, System.Drawing.Brushes.White,
                new System.Drawing.PointF(offset, offset), System.Drawing.StringFormat.GenericTypographic);
            return bitmap;
        }

        /// <summary>Measures all nonzero coverage and includes the existing transparent border; blank glyphs retain their established one-pixel placeholder.</summary>
        /// <param name="bitmap">Isolated raster read without mutation.</param>
        /// <param name="offset">Pen origin used for the blank-glyph placeholder.</param>
        /// <returns>Exact source crop expected in the packed atlas.</returns>
        static Rectangle FindCrop(Bitmap bitmap, int offset) {
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;
            for (int y = 0; y < bitmap.Height; y++) {
                for (int x = 0; x < bitmap.Width; x++) {
                    if (bitmap.GetPixel(x, y).A == 0) { continue; }
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
            if (left == int.MaxValue) { return new Rectangle(offset, offset, 1, 1); }
            return new Rectangle(left - 1, top - 1, right - left + 3, bottom - top + 3);
        }
    }
}
