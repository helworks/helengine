using System.Drawing;
using System.Drawing.Imaging;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Generates packaged font assets used by the demo-disc menu scene.
    /// </summary>
    public sealed class DemoDiscFontWriter {
        /// <summary>
        /// Glyph set rendered into each generated font atlas.
        /// </summary>
        const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?+-/()[]'\"&";

        /// <summary>
        /// Horizontal padding inserted around each rendered glyph.
        /// </summary>
        const int GlyphPadding = 6;

        /// <summary>
        /// Writes one packaged font asset to disk.
        /// </summary>
        /// <param name="outputPath">Destination font asset path.</param>
        /// <param name="fontFamilyName">Installed font family used for glyph rasterization.</param>
        /// <param name="pixelSize">Font size in pixels.</param>
        /// <param name="fontStyle">Font style used while drawing the atlas.</param>
        public void WriteFont(string outputPath, string fontFamilyName, float pixelSize, FontStyle fontStyle) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }
            if (string.IsNullOrWhiteSpace(fontFamilyName)) {
                throw new ArgumentException("Font family name must be provided.", nameof(fontFamilyName));
            }
            if (pixelSize <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(pixelSize), "Font size must be positive.");
            }

            string directoryPath = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using Font font = new Font(fontFamilyName, pixelSize, fontStyle, GraphicsUnit.Pixel);
            FontAtlasLayout layout = BuildLayout(font);
            using Bitmap bitmap = RenderAtlas(font, layout);
            FontAsset fontAsset = BuildFontAsset(fontFamilyName, font, bitmap, layout);
            using FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            helengine.files.FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Calculates glyph bounds and atlas placement for one generated font.
        /// </summary>
        /// <param name="font">Font used for measurement.</param>
        /// <returns>Resolved atlas layout.</returns>
        FontAtlasLayout BuildLayout(Font font) {
            using Bitmap measurementBitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(measurementBitmap);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            StringFormat stringFormat = StringFormat.GenericTypographic;

            List<FontGlyphLayout> glyphLayouts = new List<FontGlyphLayout>();
            int atlasWidth = 0;
            int atlasHeight = 0;
            int cursorX = GlyphPadding;
            int cursorY = GlyphPadding;
            int rowHeight = 0;
            int maximumAtlasWidth = 1024;
            for (int glyphIndex = 0; glyphIndex < Glyphs.Length; glyphIndex++) {
                char glyph = Glyphs[glyphIndex];
                SizeF measuredSize = graphics.MeasureString(glyph.ToString(), font, PointF.Empty, stringFormat);
                int glyphWidth = Math.Max(1, (int)Math.Ceiling(measuredSize.Width));
                int glyphHeight = Math.Max(1, (int)Math.Ceiling(font.GetHeight(graphics)));

                if (cursorX + glyphWidth + GlyphPadding > maximumAtlasWidth) {
                    cursorX = GlyphPadding;
                    cursorY += rowHeight + GlyphPadding;
                    rowHeight = 0;
                }

                glyphLayouts.Add(new FontGlyphLayout(
                    glyph,
                    cursorX,
                    cursorY,
                    glyphWidth,
                    glyphHeight));
                cursorX += glyphWidth + GlyphPadding;
                if (glyphHeight > rowHeight) {
                    rowHeight = glyphHeight;
                }
                if (cursorX > atlasWidth) {
                    atlasWidth = cursorX;
                }
            }

            atlasHeight = cursorY + rowHeight + GlyphPadding;
            return new FontAtlasLayout(Math.Max(atlasWidth, 64), Math.Max(atlasHeight, 64), glyphLayouts.ToArray());
        }

        /// <summary>
        /// Renders one font atlas bitmap using the resolved glyph layout.
        /// </summary>
        /// <param name="font">Font used for glyph rasterization.</param>
        /// <param name="layout">Atlas layout to render.</param>
        /// <returns>Rendered atlas bitmap.</returns>
        Bitmap RenderAtlas(Font font, FontAtlasLayout layout) {
            Bitmap bitmap = new Bitmap(layout.Width, layout.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            StringFormat stringFormat = StringFormat.GenericTypographic;
            using SolidBrush brush = new SolidBrush(Color.White);
            for (int glyphIndex = 0; glyphIndex < layout.Glyphs.Length; glyphIndex++) {
                FontGlyphLayout glyphLayout = layout.Glyphs[glyphIndex];
                graphics.DrawString(
                    glyphLayout.Character.ToString(),
                    font,
                    brush,
                    new PointF(glyphLayout.X, glyphLayout.Y),
                    stringFormat);
            }

            return bitmap;
        }

        /// <summary>
        /// Converts a rendered atlas bitmap into a packaged font asset.
        /// </summary>
        /// <param name="fontFamilyName">Friendly font family name stored in the asset metadata.</param>
        /// <param name="font">Font used for glyph rasterization.</param>
        /// <param name="bitmap">Rendered atlas bitmap.</param>
        /// <param name="layout">Atlas layout used for glyph metadata.</param>
        /// <returns>Packaged font asset.</returns>
        FontAsset BuildFontAsset(string fontFamilyName, Font font, Bitmap bitmap, FontAtlasLayout layout) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (int glyphIndex = 0; glyphIndex < layout.Glyphs.Length; glyphIndex++) {
                FontGlyphLayout glyphLayout = layout.Glyphs[glyphIndex];
                characters[glyphLayout.Character] = new FontChar(
                    new float4(glyphLayout.X, glyphLayout.Y, glyphLayout.Width, glyphLayout.Height),
                    0f,
                    glyphLayout.Width,
                    0f,
                    0f);
            }

            TextureAsset textureAsset = BuildTextureAsset(bitmap);
            FontAsset fontAsset = new FontAsset(
                new FontInfo(fontFamilyName, (int)Math.Round(font.Size), Math.Max(4f, font.Size * 0.32f)),
                new ManagedRuntimeTexture {
                    Width = bitmap.Width,
                    Height = bitmap.Height
                },
                characters,
                font.Size,
                bitmap.Width,
                bitmap.Height) {
                SourceTextureAsset = textureAsset
            };
            return fontAsset;
        }

        /// <summary>
        /// Converts one rendered bitmap into a texture asset payload.
        /// </summary>
        /// <param name="bitmap">Bitmap to convert.</param>
        /// <returns>Texture asset containing the rendered atlas pixels.</returns>
        TextureAsset BuildTextureAsset(Bitmap bitmap) {
            byte[] colors = new byte[bitmap.Width * bitmap.Height * 4];
            int writeIndex = 0;
            for (int y = 0; y < bitmap.Height; y++) {
                for (int x = 0; x < bitmap.Width; x++) {
                    Color color = bitmap.GetPixel(x, y);
                    colors[writeIndex] = color.R;
                    colors[writeIndex + 1] = color.G;
                    colors[writeIndex + 2] = color.B;
                    colors[writeIndex + 3] = color.A;
                    writeIndex += 4;
                }
            }

            return new TextureAsset {
                Width = (ushort)bitmap.Width,
                Height = (ushort)bitmap.Height,
                Colors = colors
            };
        }

    }
}
