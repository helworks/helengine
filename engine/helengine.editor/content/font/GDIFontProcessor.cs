using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Windows.Forms;
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
    [SupportedOSPlatform("windows")]
    public static class GDIFontProcessor {
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

        private static void GenerateChar(char c,
            Font font,
            int res,
            int offset,
            out Bitmap bitmap,
            out Rectangle rectangle) {
            bitmap = new Bitmap(res, res);
            string cs = c.ToString();

            int pos = (int)(res * 0.1f);
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

        public static FontAsset ImportFont(Font font) {
            Dictionary<char, TempFontChar> tempChars = new Dictionary<char, TempFontChar>();

            int res = font.Height;
            int offset = (int)(font.Height * 0.1f);

            for (int i = 0; i < Characters.Length; i++) {
                char c = Characters[i];

                Bitmap bmp;
                Rectangle rect;
                GenerateChar(c, font, res * 2, offset, out bmp, out rect);

                tempChars.Add(c, new TempFontChar(new int4(rect.X, rect.Y, rect.Width, rect.Height), bmp, 0));
            }

            Dictionary<char, FontChar> packedChars;
            Bitmap atlasImg = GenerateAtlas(tempChars, out packedChars);

            LockBitmap locker = new LockBitmap(atlasImg);
            locker.LockBits();
            locker.UnlockBits(false);

            byte[] colors = locker.Pixels;

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

            TextureAsset rawTex = new TextureAsset();
            rawTex.Colors = colors;
            rawTex.Width = (ushort)atlasImg.Width;
            rawTex.Height = (ushort)atlasImg.Height;

            RuntimeTexture asset = Core.Instance.RenderManager.BuildTextureFromRaw(rawTex);

            float spaceWidth;
            using (Bitmap bmp = new Bitmap(1, 1)) {
                using (Graphics g = Graphics.FromImage(bmp)) {
                    SizeF size = g.MeasureString(" ", font);
                    spaceWidth = size.Width;
                }
            }

            return new FontAsset(
                    new FontInfo(font.Name, 0, spaceWidth),
                    asset,
                    packedChars,
                    0.0f
                );
        }

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

        private static (int width, int height) CalculateOptimalAtlasSize(List<TempFontItem> items) {
            const int maxSize = 2048; // Maximum dimension for old consoles
            int maxItemWidth = items.Max(i => i.Width);
            int totalArea = items.Sum(i => i.Width * i.Height);

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

        private static int CalculateRequiredHeight(List<TempFontItem> items, int atlasWidth) {
            int currentX = 0, currentY = 0, rowHeight = 0, totalHeight = 0;

            foreach (var item in items) {
                if (currentX + item.Width > atlasWidth) {
                    currentY += rowHeight;
                    currentX = 0;
                    rowHeight = 0;
                }

                rowHeight = Math.Max(rowHeight, item.Height);
                currentX += item.Width;
                totalHeight = currentY + rowHeight;
            }

            return totalHeight;
        }

        private static Dictionary<char, Point> CalculatePositions(List<TempFontItem> items, int atlasWidth) {
            var positions = new Dictionary<char, Point>();

            int currentX = 0, currentY = 0, rowHeight = 0;

            foreach (var item in items) {
                if (currentX + item.Width > atlasWidth) {
                    currentY += rowHeight;
                    currentX = 0;
                    rowHeight = 0;
                }

                positions[item.Char] = new Point(currentX, currentY);
                rowHeight = Math.Max(rowHeight, item.Height);
                currentX += item.Width;
            }

            return positions;
        }

        private static Bitmap CreateAtlasBitmap(List<TempFontItem> items, Dictionary<char, Point> positions, int width, int height) {
            Bitmap atlas = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(atlas)) {
                g.Clear(Color.Transparent);
                foreach (var item in items) {
                    Point pos = positions[item.Char];
                    g.DrawImage(item.Bitmap, pos.X, pos.Y, new Rectangle(item.X, item.Y, item.Width, item.Height), GraphicsUnit.Pixel);
                }
            }
            return atlas;
        }

        private static Dictionary<char, FontChar> CreateUpdatedDictionary(
            Dictionary<char, TempFontChar> original,
            Dictionary<char, Point> positions,
            Bitmap atlas) {
            var newDict = new Dictionary<char, FontChar>();

            foreach (var kvp in original) {
                var pos = positions[kvp.Key];
                var originalChar = kvp.Value;

                newDict[kvp.Key] = new FontChar(
                    new float4(pos.X / (float)atlas.Width,
                        pos.Y / (float)atlas.Height,
                        originalChar.SourceRect.Z / (float)atlas.Width,
                        originalChar.SourceRect.W / (float)atlas.Height
                    ),
                    originalChar.OffsetY
                );
            }

            return newDict;
        }
    }
}