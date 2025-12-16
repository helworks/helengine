using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Bitmap = System.Drawing.Bitmap;
using Brushes = System.Drawing.Brushes;
using Font = System.Drawing.Font;
using Graphics = System.Drawing.Graphics;
using GraphicsUnit = System.Drawing.GraphicsUnit;
using PointF = System.Drawing.PointF;
using SmoothingMode = System.Drawing.Drawing2D.SmoothingMode;
using StringFormat = System.Drawing.StringFormat;
using TextRenderingHint = System.Drawing.Text.TextRenderingHint;

namespace helengine.editor {
    /// <summary>
    /// Provides GDI-based font processing to build glyph atlases and font assets.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class GDIFontProcessor {
        // Extra transparent pixels around each glyph in the atlas to prevent bleeding
        private const int ATLAS_PADDING = 2;
        static readonly char[] Characters = new char[]
        {
            'a', 'à', 'á', 'ã', 'b', 'c', 'd', 'e', 'é', 'ê',
            'f', 'g', 'h', 'i', 'í', 'j',
            'k', 'l', 'm', 'n', 'o', 'ó', 'ô', 'õ',
            'p', 'q', 'r', 's', 't',
            'u', 'ú', 'v', 'w', 'x', 'y', 'z',
            'A', 'À', 'Á', 'Ã', 'B', 'C', 'D', 'E', 'É', 'Ê',
            'F', 'G', 'H', 'I', 'Í', 'J',
            'K', 'L', 'M', 'N', 'O', 'Ó', 'Ô', 'Õ',
            'P', 'Q', 'R', 'S', 'T',
            'U', 'Ú', 'V', 'W', 'X', 'Y', 'Z', 'Ç', 'ç',

            // numbers
            '1', '2', '3', '4', '5', '6', '7', '8', '9', '0',

            // symbols
            '!', '@', '#', '$', '%', '^', '&', '*',
            '(', ')', '-', '_', '=', '+', '?', ',', '.',
            '/', @"\"[0], ':', ';', '|',
            '{', '}', '~', '`', "'"[0]
        };

        /// <summary>
        /// Generates a glyph bitmap and bounds for the specified character.
        /// </summary>
        /// <param name="c">Character to generate.</param>
        /// <param name="font">Font used for rendering.</param>
        /// <param name="res">Resolution of the temporary bitmap.</param>
        /// <param name="offset">Padding offset applied before rendering.</param>
        /// <param name="bitmap">Output bitmap containing the rendered glyph.</param>
        /// <param name="rectangle">Output bounds of the glyph within the bitmap.</param>
        private static void GenerateChar(char c,
            Font font,
            int res,
            int offset,
            out Bitmap bitmap,
            out Rectangle rectangle) {
            bitmap = new Bitmap(res, res);
            string cs = c.ToString();

            int pos = offset; // consistent top-of-line margin
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                graphics.DrawString(cs, font, Brushes.White, new PointF(pos, pos), StringFormat.GenericTypographic);
            }

            LockBitmap locker = new LockBitmap(bitmap);
            locker.LockBits();

            byte[,] bytes = new byte[res, res];
            for (int x = 0; x < bitmap.Width; x++) {
                for (int y = 0; y < bitmap.Height; y++) {
                    Color pixel = locker.GetPixel(x, y);
                    bytes[x, y] = pixel.R;
                }
            }

            locker.UnlockBits(false);
            locker = null;

            int X = int.MaxValue;
            int Y = int.MaxValue;
            int Right = int.MinValue;
            int Bottom = int.MinValue;

            for (int x = 0; x < res; x++) {
                for (int y = 0; y < res; y++) {
                    byte p = bytes[x, y];

                    if (p != 0) {
                        X = Math.Min(X, x);
                        Y = Math.Min(Y, y);
                        Right = Math.Max(Right, x);
                        Bottom = Math.Max(Bottom, y);
                    }
                }
            }

            int Width = (Right - X) + 1;
            int Height = (Bottom - Y) + 1;

            int off = 1;
            rectangle = new Rectangle(X - off, Y - off, Width + off, Height + off);
        }

        /// <summary>
        /// Generates a font asset and atlas for the supplied font using GDI-based rendering.
        /// </summary>
        /// <param name="font">Font to import.</param>
        /// <returns>Constructed <see cref="FontAsset"/> containing atlas texture and metrics.</returns>
        public static FontAsset ImportFont(Font font) {
            Dictionary<char, TempFontChar> tempChars = new Dictionary<char, TempFontChar>();

            // Measure context for metrics (96 DPI by default)
            float lineHeightPx;
            float spaceWidth;
            using (Bitmap bmpMeasure = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(bmpMeasure)) {
                // Prefer typographic metrics and trailing spaces
                using (var fmt = new StringFormat(StringFormat.GenericTypographic)) {
                    fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                    spaceWidth = g.MeasureString(" ", font, PointF.Empty, fmt).Width;
                }

                lineHeightPx = font.GetHeight(g);
            }

            int res = Math.Max(16, (int)Math.Ceiling(lineHeightPx));
            int offset = (int)(res * 0.1f);

            // Build glyph bitmaps and capture advance widths
            for (int i = 0; i < Characters.Length; i++) {
                char c = Characters[i];

                Bitmap bmp;
                Rectangle rect;
                GenerateChar(c, font, res * 2, offset, out bmp, out rect);

                // Measure advance width with typographic settings
                float advanceWidth;
                using (Bitmap bmpMeasure = new Bitmap(1, 1))
                using (Graphics g = Graphics.FromImage(bmpMeasure))
                using (var fmt = new StringFormat(StringFormat.GenericTypographic)) {
                    fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                    advanceWidth = g.MeasureString(c.ToString(), font, PointF.Empty, fmt).Width;
                }

                // Vertical offset from line-top to glyph-top in pixels
                float glyphOffsetY = rect.Y - offset;

                tempChars[c] = new TempFontChar(
                    new int4(rect.X, rect.Y, rect.Width, rect.Height),
                    bmp,
                    glyphOffsetY,
                    advanceWidth,
                    0,                 // BearingX (not computed with GDI+)
                    0                  // BearingY
                );
            }

            Dictionary<char, FontChar> packedChars;
            Bitmap atlasImg = GenerateAtlas(tempChars, out packedChars);

            LockBitmap locker = new LockBitmap(atlasImg);
            locker.LockBits();
            locker.UnlockBits(false);

            byte[] colors = locker.Pixels;

            // Convert ARGB->RGBA as expected by renderer
            for (int i = 0; i < colors.Length; i += 4) {
                byte a = colors[i];
                byte r = colors[i + 1];
                byte g = colors[i + 2];
                byte b = colors[i + 3];

                colors[i] = r;
                colors[i + 1] = g;
                colors[i + 2] = b;
                colors[i + 3] = a;
            }

            // DEBUG: Save atlas to disk to inspect
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string safeName = new string(font.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
                string file = Path.Combine(baseDir, $"font_atlas_{safeName}_{(int)Math.Ceiling(lineHeightPx)}.png");
                atlasImg.Save(file, ImageFormat.Png);
            } catch { /* ignore IO issues */ }

            TextureAsset rawTex = new TextureAsset();
            rawTex.Colors = colors;
            rawTex.Width = (ushort)atlasImg.Width;
            rawTex.Height = (ushort)atlasImg.Height;

            RuntimeTexture asset = Core.Instance.RenderManager2D.BuildTextureFromRaw(rawTex);

            // Populate font asset with measured line height and space width
            return new FontAsset(
                new FontInfo(font.Name, (int)Math.Ceiling(lineHeightPx), spaceWidth),
                asset,
                packedChars,
                lineHeightPx,
                atlasImg.Width,
                atlasImg.Height
            );
        }

        /// <summary>
        /// Builds a packed atlas bitmap from the temporary glyph data.
        /// </summary>
        /// <param name="tempChars">Source glyphs and metrics.</param>
        /// <param name="packedChars">Resulting packed glyph metadata.</param>
        /// <returns>Generated atlas bitmap.</returns>
        private static Bitmap GenerateAtlas(Dictionary<char, TempFontChar> tempChars, out Dictionary<char, FontChar> packedChars) {
            // Extract and sort items
            var items = tempChars.Select(kvp => new TempFontItem {
                Char = kvp.Key,
                X = kvp.Value.SourceRect.X,
                Y = kvp.Value.SourceRect.Y,
                Width = kvp.Value.SourceRect.Z,
                Height = kvp.Value.SourceRect.W,
                Bitmap = kvp.Value.Bitmap
            }).OrderByDescending(i => i.Height).ToList();

            // Find optimal atlas size
            (int width, int height) = CalculateOptimalAtlasSize(items);

            // Calculate positions
            var positions = CalculatePositions(items, width);

            // Create atlas bitmap
            Bitmap atlas = CreateAtlasBitmap(items, positions, width, height);

            // Create new dictionary with updated references
            packedChars = CreateUpdatedDictionary(tempChars, positions, atlas);

            return atlas;
        }

        /// <summary>
        /// Calculates an optimal power-of-two atlas size for the given glyphs.
        /// </summary>
        /// <param name="items">Glyph items with dimensions.</param>
        /// <returns>Width and height for the atlas.</returns>
        private static (int width, int height) CalculateOptimalAtlasSize(List<TempFontItem> items) {
            const int maxSize = 2048; // Maximum dimension for old consoles
            int maxItemWidth = items.Max(i => i.Width + (ATLAS_PADDING * 2));
            int totalArea = items.Sum(i => (i.Width + (ATLAS_PADDING * 2)) * (i.Height + (ATLAS_PADDING * 2)));

            // Try different POT sizes starting from minimum required
            var candidates = new List<(int w, int h)>();
            for (int pot = 32; pot <= maxSize; pot *= 2) {
                if (pot < maxItemWidth) continue;

                int height = CalculateRequiredHeight(items, pot);
                if (height <= pot) // Only consider square-ish POT sizes
                {
                    candidates.Add((pot, height));
                }
            }

            // Find the smallest POT that fits everything
            var best = candidates
                .OrderBy(c => Math.Max(c.w, c.h)) // Prefer smaller maximum dimension
                .ThenBy(c => c.w * c.h)           // Then by area
                .FirstOrDefault();

            // Fallback if no POT fits - use minimal possible
            if (best.w == 0) {
                best = (maxSize, CalculateRequiredHeight(items, maxSize));
                best.h = Math.Min(best.h, maxSize);
            }

            // Final safety check
            return (NextPowerOfTwo(best.w), NextPowerOfTwo(best.h));
        }

        /// <summary>
        /// Finds the next power-of-two greater than or equal to <paramref name="n"/>.
        /// </summary>
        /// <param name="n">Input value.</param>
        /// <returns>Next power-of-two.</returns>
        private static int NextPowerOfTwo(int n) {
            if (n < 1) return 1;
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            return n + 1;
        }

        /// <summary>
        /// Estimates the required atlas height for the given width using row packing.
        /// </summary>
        /// <param name="items">Glyph items to pack.</param>
        /// <param name="atlasWidth">Candidate atlas width.</param>
        /// <returns>Estimated height to fit all glyphs.</returns>
        private static int CalculateRequiredHeight(List<TempFontItem> items, int atlasWidth) {
            int currentX = 0, currentY = 0, rowHeight = 0, totalHeight = 0;

            foreach (var item in items) {
                int paddedWidth = item.Width + (ATLAS_PADDING * 2);
                int paddedHeight = item.Height + (ATLAS_PADDING * 2);

                if (currentX + paddedWidth > atlasWidth) {
                    currentY += rowHeight;
                    currentX = 0;
                    rowHeight = 0;
                }

                rowHeight = Math.Max(rowHeight, paddedHeight);
                currentX += paddedWidth;
                totalHeight = currentY + rowHeight;
            }

            return totalHeight;
        }

        /// <summary>
        /// Calculates top-left positions for each glyph within the atlas.
        /// </summary>
        /// <param name="items">Glyph items to position.</param>
        /// <param name="atlasWidth">Width of the atlas.</param>
        /// <returns>Dictionary mapping characters to their atlas positions.</returns>
        private static Dictionary<char, Point> CalculatePositions(List<TempFontItem> items, int atlasWidth) {
            var positions = new Dictionary<char, Point>();

            int currentX = 0, currentY = 0, rowHeight = 0;

            foreach (var item in items) {
                int paddedWidth = item.Width + (ATLAS_PADDING * 2);
                int paddedHeight = item.Height + (ATLAS_PADDING * 2);

                if (currentX + paddedWidth > atlasWidth) {
                    currentY += rowHeight;
                    currentX = 0;
                    rowHeight = 0;
                }

                // Store the slot top-left (including padding). We'll add padding when drawing and when computing UVs
                positions[item.Char] = new Point(currentX, currentY);
                rowHeight = Math.Max(rowHeight, paddedHeight);
                currentX += paddedWidth;
            }

            return positions;
        }

        /// <summary>
        /// Renders the atlas bitmap from glyph bitmaps and calculated positions.
        /// </summary>
        /// <param name="items">Glyph items to draw.</param>
        /// <param name="positions">Positions of each glyph in the atlas.</param>
        /// <param name="width">Atlas width.</param>
        /// <param name="height">Atlas height.</param>
        /// <returns>Rendered atlas bitmap.</returns>
        private static Bitmap CreateAtlasBitmap(List<TempFontItem> items, Dictionary<char, Point> positions, int width, int height) {
            Bitmap atlas = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(atlas)) {
                g.Clear(Color.Transparent);
                foreach (var item in items) {
                    Point pos = positions[item.Char];
                    // Draw glyph with padding offset to leave transparent border around glyph
                    g.DrawImage(item.Bitmap, pos.X + ATLAS_PADDING, pos.Y + ATLAS_PADDING,
                        new Rectangle(item.X, item.Y, item.Width, item.Height), GraphicsUnit.Pixel);
                }
            }
            return atlas;
        }

        /// <summary>
        /// Produces packed font character metadata using atlas positions.
        /// </summary>
        /// <param name="original">Original glyph metrics.</param>
        /// <param name="positions">Calculated atlas positions.</param>
        /// <param name="atlas">Atlas bitmap.</param>
        /// <returns>Dictionary of packed font characters.</returns>
        private static Dictionary<char, FontChar> CreateUpdatedDictionary(
            Dictionary<char, TempFontChar> original,
            Dictionary<char, Point> positions,
            Bitmap atlas) {
            var newDict = new Dictionary<char, FontChar>();

            foreach (var kvp in original) {
                var pos = positions[kvp.Key];
                var originalChar = kvp.Value;

                newDict[kvp.Key] = new FontChar(
                    new float4(
                        (pos.X + ATLAS_PADDING) / (float)atlas.Width,
                        (pos.Y + ATLAS_PADDING) / (float)atlas.Height,
                        originalChar.SourceRect.Z / (float)atlas.Width,
                        originalChar.SourceRect.W / (float)atlas.Height
                    ),
                    originalChar.OffsetY,
                    originalChar.AdvanceWidth,
                    originalChar.BearingX,
                    originalChar.BearingY
                );
            }

            return newDict;
        }
    }
}
