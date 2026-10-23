using System.Security.Cryptography;
using ImageMagick;
using ImageMagick.Drawing;

namespace helengine.editor {
    /// <summary>Rasterizes source font bytes through ImageMagick/FreeType on Windows, Linux and macOS.</summary>
    public sealed class PortableFontImporter : IFontImporter {
        /// <summary>Serializes access to the process-owned temporary source face independently of character requests.</summary>
        static readonly object SourceFileLock = new();

        /// <summary>Maximum atlas dimension supported by the shared font format.</summary>
        const int MaximumAtlasSize = 2048;

        /// <summary>Imports a source face with explicit size and no fallback to a host-installed font.</summary>
        /// <param name="stream">TrueType or OpenType source stream.</param>
        /// <param name="settings">Requested font metrics and raster size.</param>
        /// <returns>Font metadata and its CPU-owned straight-alpha atlas.</returns>
        public FontAsset ImportFont(Stream stream, FontAssetProcessorSettings settings) {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(settings);
            if (settings.PixelSize < 1 || settings.PixelSize > 256) {
                throw new ArgumentOutOfRangeException(nameof(settings), "Portable font pixel size must be between 1 and 256.");
            }
            settings.Characters.RequireBmpCharacters();
            using MemoryStream source = new MemoryStream();
            stream.CopyTo(source);
            byte[] bytes = source.ToArray();
            if (bytes.Length < 12 || !IsSupportedFace(bytes)) {
                throw new InvalidDataException("Font source must contain a TrueType or OpenType face.");
            }
            string identity = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string directory = OperatingSystem.IsWindows() ? @"C:\temp\helengine\font-import" : "/tmp/helengine/font-import";
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, identity + "-" + Environment.ProcessId + ".ttf");
            // Serialize imports within this process; separate processes never replace each other's face files.
            lock (SourceFileLock) {
                if (!File.Exists(file)) {
                    File.WriteAllBytes(file, bytes);
                }
                try {
                    return ImportFace(file, identity, settings);
                } finally {
                    File.Delete(file);
                }
            }
        }

        /// <summary>Checks the supported SFNT signatures before passing source data to the native font parser.</summary>
        /// <param name="bytes">Source face bytes.</param>
        /// <returns>Whether the bytes identify a supported font container.</returns>
        static bool IsSupportedFace(byte[] bytes) {
            return (bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0)
                || (bytes[0] == 'O' && bytes[1] == 'T' && bytes[2] == 'T' && bytes[3] == 'O')
                || (bytes[0] == 't' && bytes[1] == 't' && bytes[2] == 'c' && bytes[3] == 'f');
        }

        /// <summary>Measures one explicit face, rasterizes its glyphs and assembles their shared atlas.</summary>
        /// <param name="path">Path to the temporary source face.</param>
        /// <param name="identity">Stable source-byte identity used for font cache naming.</param>
        /// <param name="settings">Requested font processing settings.</param>
        /// <returns>Imported font with measured metrics.</returns>
        static FontAsset ImportFace(string path, string identity, FontAssetProcessorSettings settings) {
            using MagickImage measure = new MagickImage(MagickColors.Transparent, 1, 1);
            measure.Settings.Font = path;
            measure.Settings.FontPointsize = settings.PixelSize;
            measure.Settings.Density = new Density(72);
            ITypeMetric metrics = measure.FontTypeMetrics("Mg");
            float lineHeight = (float)Math.Max(metrics.TextHeight, metrics.Ascent - metrics.Descent);
            if (!float.IsFinite(lineHeight) || lineHeight <= 0) {
                throw new InvalidDataException("The source face did not provide valid line metrics.");
            }
            List<PortableFontGlyph> glyphs = new List<PortableFontGlyph>();
            foreach (char character in settings.Characters.RequireBmpCharacters()) {
                float advance = (float)measure.FontTypeMetrics(character.ToString()).TextWidth;
                glyphs.Add(Rasterize(path, character, settings.PixelSize, (float)metrics.Ascent, advance));
            }
            FontAsset font = PackAtlas(identity, glyphs, lineHeight, (float)measure.FontTypeMetrics(" ").TextWidth);
            FontHorizontalMetricsProcessor.Apply(font, settings);
            return font;
        }

        /// <summary>Renders one glyph, crops its alpha bounds and preserves its origin relative to the line.</summary>
        /// <param name="path">Explicit face file, never a system font alias.</param>
        /// <param name="character">Character to rasterize.</param>
        /// <param name="pixelSize">Requested pixel size at 72 DPI.</param>
        /// <param name="ascent">Distance from line top to baseline.</param>
        /// <param name="advance">Measured pen advance.</param>
        /// <returns>Cropped glyph pixels and layout metrics.</returns>
        static PortableFontGlyph Rasterize(string path, char character, int pixelSize, float ascent, float advance) {
            int size = checked(pixelSize * 4 + 16);
            int margin = pixelSize + 4;
            using MagickImage image = new MagickImage(MagickColors.Transparent, (uint)size, (uint)size);
            image.Settings.Density = new Density(72);
            new Drawables().Font(path).FontPointSize(pixelSize).FillColor(MagickColors.White)
                .StrokeColor(MagickColors.None).Text(margin, margin + ascent, character.ToString()).Draw(image);
            using IPixelCollection<byte> collection = image.GetPixels();
            byte[] rgba = collection.ToByteArray(PixelMapping.RGBA);
            int left = size, top = size, right = -1, bottom = -1;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    if (rgba[(y * size + x) * 4 + 3] == 0) { continue; }
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            }
            if (right < left) {
                return new PortableFontGlyph { Character = character, Width = 1, Height = 1, Pixels = new byte[4], Advance = advance };
            }
            left = Math.Max(0, left - 1); top = Math.Max(0, top - 1);
            right = Math.Min(size - 1, right + 1); bottom = Math.Min(size - 1, bottom + 1);
            int width = right - left + 1, height = bottom - top + 1;
            byte[] pixels = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int destination = (y * width + x) * 4;
                    pixels[destination] = pixels[destination + 1] = pixels[destination + 2] = 255;
                    pixels[destination + 3] = rgba[((y + top) * size + x + left) * 4 + 3];
                }
            }
            return new PortableFontGlyph { Character = character, Width = width, Height = height, Pixels = pixels,
                Advance = advance, BearingX = left - margin, OffsetY = top - margin };
        }

        /// <summary>Packs glyphs into rows in the smallest square power-of-two atlas that fits.</summary>
        /// <param name="identity">Stable face identity.</param>
        /// <param name="glyphs">Cropped glyph images and metrics.</param>
        /// <param name="lineHeight">Measured line height.</param>
        /// <param name="spaceWidth">Space advance measured independently of whether its glyph was requested.</param>
        /// <returns>Complete font and retained atlas pixels.</returns>
        static FontAsset PackAtlas(string identity, List<PortableFontGlyph> glyphs, float lineHeight, float spaceWidth) {
            for (int size = 32; size <= MaximumAtlasSize; size *= 2) {
                int x = 1, y = 1, rowHeight = 0;
                Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
                byte[] atlas = new byte[checked(size * size * 4)];
                foreach (PortableFontGlyph glyph in glyphs) {
                    if (glyph.Width + 2 > size) { break; }
                    if (x + glyph.Width + 1 > size) { x = 1; y += rowHeight + 1; rowHeight = 0; }
                    if (y + glyph.Height + 1 > size) { break; }
                    for (int row = 0; row < glyph.Height; row++) {
                        Buffer.BlockCopy(glyph.Pixels, row * glyph.Width * 4, atlas, ((y + row) * size + x) * 4, glyph.Width * 4);
                    }
                    characters.Add(glyph.Character, new FontChar(new float4((float)x / size, (float)y / size,
                        (float)glyph.Width / size, (float)glyph.Height / size), glyph.OffsetY, glyph.Advance, glyph.BearingX, 0));
                    x += glyph.Width + 1;
                    rowHeight = Math.Max(rowHeight, glyph.Height);
                }
                if (characters.Count != glyphs.Count) { continue; }
                TextureAsset texture = new TextureAsset { Width = (ushort)size, Height = (ushort)size, Colors = atlas };
                return new FontAsset(new FontInfo(identity, (int)Math.Ceiling(lineHeight), spaceWidth),
                    new PortableFontRuntimeTexture { Width = size, Height = size }, characters, lineHeight, size, size) { SourceTextureAsset = texture };
            }
            throw new InvalidOperationException("The font glyphs do not fit the supported 2048-pixel atlas.");
        }
    }
}
