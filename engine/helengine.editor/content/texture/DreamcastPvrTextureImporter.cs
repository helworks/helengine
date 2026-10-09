using System.Buffers.Binary;

namespace helengine.editor {
    /// <summary>Imports Sega PVRT textures, including twiddled rectangles, mipmaps, VQ and small VQ.</summary>
    public sealed class DreamcastPvrTextureImporter : ITextureImporter {
        /// <summary>Imports the highest-resolution image; paletted FileStreams use a sibling KallistiOS DPAL .pal file.</summary>
        public TextureAsset ImportTexture(Stream stream) {
            return ImportTexture(stream, null);
        }

        /// <summary>Imports a PVRT image with an explicit RGBA palette when its indices reference an external palette.</summary>
        public TextureAsset ImportTexture(Stream stream, byte[] rgbaPalette) {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using MemoryStream copy = new();
            // Hardware textures fit in a few MiB; reject oversized input before allocating unbounded buffers.
            byte[] buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) != 0) {
                if (copy.Length + read > 8 * 1024 * 1024) throw new InvalidDataException("Dreamcast PVR texture exceeds 8 MiB.");
                copy.Write(buffer, 0, read);
            }
            byte[] bytes = copy.ToArray();
            int start = 0;
            if (bytes.Length >= 8 && Read32(bytes, 0) == 0x58494247) {
                uint gbixLength = Read32(bytes, 4);
                if (gbixLength > (uint)(bytes.Length - 8)) throw new InvalidDataException("Truncated GBIX chunk.");
                start = checked(8 + (int)gbixLength);
            }
            if (bytes.Length - start < 16 || Read32(bytes, start) != 0x54525650) throw new InvalidDataException("Dreamcast .PVR requires a PVRT chunk.");
            uint chunkLength = Read32(bytes, start + 4);
            int available = bytes.Length - start;
            // Sega counts bytes after FourCC/length; KOS pvrtex also emits a whole-chunk length.
            if (chunkLength != available - 8 && chunkLength != available) throw new InvalidDataException("PVRT chunk length does not match the file.");
            int filePixelFormat = bytes[start + 8], storage = bytes[start + 9];
            // PVRT storage codes, rather than the palette-entry pixel format, determine index depth.
            int format = storage == 5 || storage == 6 ? 5 : storage == 7 || storage == 8 ? 6 : filePixelFormat;
            int width = Read16(bytes, start + 12), height = Read16(bytes, start + 14);
            if (filePixelFormat > 6 || width < 1 || height < 1 || width > 1024 || height > 1024 || (storage == 11 ? width % 32 != 0 : (width & (width - 1)) != 0) || (height & (height - 1)) != 0) throw new InvalidDataException("Invalid PVR format or dimensions.");
            bool linear = storage == 9 || storage == 11;
            bool mipmaps = storage == 2 || storage == 4 || storage == 6 || storage == 8 || storage == 17 || storage == 18;
            bool vq = storage == 3 || storage == 4 || storage == 16 || storage == 17;
            if (storage != 1 && storage != 2 && storage != 3 && storage != 4 && storage != 5 && storage != 6 && storage != 7 && storage != 8 && storage != 9 && storage != 11 && storage != 13 && storage != 16 && storage != 17 && storage != 18) throw new InvalidDataException($"Unsupported PVR storage layout {storage}.");
            if ((mipmaps || vq || storage == 1) && width != height) throw new InvalidDataException("This PVR layout requires a square texture.");
            bool paletted = storage >= 5 && storage <= 8;
            if (vq && (format >= 6 || width < 2)) throw new InvalidDataException("PVR pixel format conflicts with its storage layout.");
            if (format == 3 && width < 2) throw new InvalidDataException("YUV422 requires horizontal pixel pairs.");
            if (paletted) {
                if (rgbaPalette == null && stream is FileStream file) rgbaPalette = ReadCompanionPalette(file.Name);
                int capacity = format == 5 ? 16 : 256;
                if (rgbaPalette == null || rgbaPalette.Length == 0 || rgbaPalette.Length % 4 != 0 || rgbaPalette.Length > capacity * 4) throw new InvalidDataException($"Paletted PVR requires an external RGBA palette (up to {capacity} colors) or a sibling DPAL .pal file.");
            }
            int entries = 256;
            if (storage == 16 || storage == 17) entries = width <= 16 ? 16 : width <= 32 ? (mipmaps ? 64 : 32) : width <= 64 ? (mipmaps ? 256 : 128) : 256;
            int payloadStart = start + 16;
            int payloadLength = bytes.Length - payloadStart;
            int addressFormat = paletted ? format : format >= 5 ? 0 : format;
            int expected = DreamcastPvrPixelCodec.PayloadBytes(width, height, addressFormat, mipmaps, vq, entries);
            if (paletted && format == 5 && !mipmaps) expected = Math.Max(1, expected);
            if (!paletted && format == 6) expected *= 2;
            // Legacy non-VQ files may omit the unused mip prefix. Only the documented prefix differences are allowed.
            int missingPrefix = expected - payloadLength;
            int maximumMissingPrefix = paletted ? format == 5 ? 1 : 2 : format == 6 ? 8 : 4;
            if (missingPrefix != 0 && (!mipmaps || vq || missingPrefix != maximumMissingPrefix && missingPrefix != 4)) throw new InvalidDataException("PVR pixel payload has an invalid or truncated length.");
            int mipOffset = mipmaps ? DreamcastPvrPixelCodec.MipOffset(width, addressFormat, vq) : 0;
            if (!paletted && format == 6) mipOffset *= 2;
            int imageStart = payloadStart + (vq ? entries * 8 : 0) + mipOffset - missingPrefix;
            if (imageStart < payloadStart || imageStart >= bytes.Length) throw new InvalidDataException("PVR mip prefix truncates the image.");
            byte[] rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int address = PixelAddress(bytes, imageStart, payloadStart, x, y, width, height, addressFormat, linear, vq, entries);
                    int destination = (y * width + x) * 4;
                    if (paletted) {
                        if (format == 5 && mipmaps && width == 1 && height == 1) address++;
                        int index = format == 5 ? (bytes[address / 2] >> ((address & 1) * 4)) & 15 : bytes[address];
                        if ((index + 1) * 4 > rgbaPalette.Length) throw new InvalidDataException("PVR index exceeds the supplied palette.");
                        Buffer.BlockCopy(rgbaPalette, index * 4, rgba, destination, 4);
                    } else if (format == 3) {
                        int partner = PixelAddress(bytes, imageStart, payloadStart, x ^ 1, y, width, height, format, linear, vq, entries);
                        ushort word = Read16(bytes, address), other = Read16(bytes, partner);
                        int u = ((x & 1) == 0 ? word : other) & 255;
                        int v = ((x & 1) == 0 ? other : word) & 255;
                        DreamcastPvrPixelCodec.DecodeYuvWord(word, u - 128, v - 128, rgba, destination);
                    } else if (format == 6) {
                        address = imageStart + (address - imageStart) * 2;
                        rgba[destination] = bytes[address + 2]; rgba[destination + 1] = bytes[address + 1]; rgba[destination + 2] = bytes[address]; rgba[destination + 3] = bytes[address + 3];
                    } else {
                        ushort word = Read16(bytes, address);
                        if (format == 5) word |= 0x8000;
                        DreamcastPvrPixelCodec.DecodeWord(word, format == 5 ? 0 : format, rgba, destination);
                    }
                }
            }
            return new TextureAsset { Width = (ushort)width, Height = (ushort)height, ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = rgba, PaletteColors = new byte[0] };
        }

        /// <summary>Resolves a byte address, or an absolute nibble address for PAL4, and validates VQ indices.</summary>
        static int PixelAddress(byte[] bytes, int imageStart, int codebookStart, int x, int y, int width, int height, int format, bool linear, bool vq, int entries) {
            int index = linear ? y * width + x : DreamcastPvrPixelCodec.TwiddledIndex(x, y, width, height);
            if (vq) {
                int vector = bytes[imageStart + DreamcastPvrPixelCodec.TwiddledIndex(x / 2, y / 2, width / 2, height / 2)];
                if (vector >= entries) throw new InvalidDataException("PVR VQ index exceeds its codebook.");
                return codebookStart + vector * 8 + ((x & 1) * 2 + (y & 1)) * 2;
            }
            return format == 5 ? imageStart * 2 + index : imageStart + index * (format == 6 ? 1 : 2);
        }

        /// <summary>Reads the KallistiOS DPAL palette beside the PVR and converts little-endian ARGB to RGBA.</summary>
        static byte[] ReadCompanionPalette(string path) {
            string palettePath = Path.ChangeExtension(path, ".pal");
            if (!File.Exists(palettePath)) return null;
            byte[] bytes = File.ReadAllBytes(palettePath);
            if (bytes.Length < 8 || Read32(bytes, 0) != 0x4c415044) throw new InvalidDataException("Companion .pal requires a DPAL header.");
            uint count = Read32(bytes, 4);
            if (count < 1 || count > 256 || bytes.Length != 8 + count * 4) throw new InvalidDataException("Invalid DPAL palette length.");
            byte[] rgba = new byte[count * 4];
            for (int offset = 0; offset < rgba.Length; offset += 4) {
                rgba[offset] = bytes[8 + offset + 2]; rgba[offset + 1] = bytes[8 + offset + 1]; rgba[offset + 2] = bytes[8 + offset]; rgba[offset + 3] = bytes[8 + offset + 3];
            }
            return rgba;
        }

        /// <summary>Reads a checked little-endian word without depending on host byte order.</summary>
        static ushort Read16(byte[] bytes, int offset) { return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2)); }

        /// <summary>Reads a checked little-endian chunk field.</summary>
        static uint Read32(byte[] bytes, int offset) { return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)); }
    }
}
