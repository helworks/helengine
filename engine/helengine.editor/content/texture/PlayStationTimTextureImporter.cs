namespace helengine.editor {
    /// <summary>Imports standard uncompressed PS1 TIM images in 4-, 8-, 16- and 24-bit modes as straight RGBA32.</summary>
    public sealed class PlayStationTimTextureImporter : ITextureImporter {
        /// <summary>Limits each section to one complete PS1 VRAM image before allocating its payload.</summary>
        const int MaximumSectionBytes = 1024 * 512 * 2;

        /// <summary>Imports the first palette of an indexed TIM, or the direct-color image.</summary>
        /// <param name="stream">Readable stream positioned at the TIM header; it remains open.</param>
        /// <returns>Decoded RGBA32 image for the ordinary editor import pipeline.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            return ImportTexture(stream, 0);
        }

        /// <summary>Imports an image using a selected palette bank; VRAM destination coordinates do not relocate its pixels.</summary>
        /// <param name="stream">Readable stream positioned at the TIM header; seeking is unnecessary.</param>
        /// <param name="paletteIndex">Zero-based bank of 16 or 256 consecutive CLUT entries.</param>
        /// <returns>Decoded RGBA32 image.</returns>
        public TextureAsset ImportTexture(Stream stream, int paletteIndex) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (!stream.CanRead) {
                throw new ArgumentException("TIM source stream must be readable.", nameof(stream));
            } else if (paletteIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(paletteIndex));
            }

            using BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
            if (reader.ReadUInt32() != 0x10) {
                throw new InvalidDataException("Invalid TIM signature or unsupported compressed TIM header.");
            }
            uint flags = reader.ReadUInt32();
            int mode = (int)(flags & 7);
            bool hasPalette = (flags & 8) != 0;
            if ((flags & ~15u) != 0 || mode > 3) {
                throw new InvalidDataException("TIM requires a standard 4-, 8-, 16- or 24-bit image mode.");
            } else if (mode < 2 && !hasPalette) {
                throw new InvalidDataException("Indexed TIM images require an embedded CLUT.");
            } else if (mode >= 2 && paletteIndex != 0) {
                throw new ArgumentOutOfRangeException(nameof(paletteIndex), "Direct-color TIM images have no palette banks.");
            }

            int paletteWidth = 0;
            int paletteHeight = 0;
            byte[] palette = hasPalette ? ReadSection(reader, out paletteWidth, out paletteHeight) : Array.Empty<byte>();
            int entriesPerPalette = mode == 0 ? 16 : 256;
            if (mode < 2 && (paletteWidth % entriesPerPalette != 0 || paletteIndex >= palette.Length / (entriesPerPalette * 2))) {
                throw new InvalidDataException("TIM CLUT dimensions or selected palette bank are invalid.");
            }

            byte[] pixels = ReadSection(reader, out int widthInWords, out int height);
            int rowBytes = widthInWords * 2;
            int width = mode == 0 ? widthInWords * 4 : mode == 1 ? widthInWords * 2 : mode == 2 ? widthInWords : rowBytes / 3;
            if (width == 0) {
                throw new InvalidDataException("TIM rows must contain at least one complete pixel.");
            }
            byte[] rgba = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int outputOffset = (y * width + x) * 4;
                    int rowOffset = y * rowBytes;
                    if (mode < 2) {
                        int index = mode == 0 ? (pixels[rowOffset + x / 2] >> ((x % 2) * 4)) & 15 : pixels[rowOffset + x];
                        int paletteOffset = (paletteIndex * entriesPerPalette + index) * 2;
                        Ps1TexturePixelCodec.DecodePixel(ReadWord(palette, paletteOffset), rgba, outputOffset);
                    } else if (mode == 2) {
                        Ps1TexturePixelCodec.DecodePixel(ReadWord(pixels, rowOffset + x * 2), rgba, outputOffset);
                    } else {
                        int inputOffset = rowOffset + x * 3;
                        rgba[outputOffset] = pixels[inputOffset];
                        rgba[outputOffset + 1] = pixels[inputOffset + 1];
                        rgba[outputOffset + 2] = pixels[inputOffset + 2];
                        rgba[outputOffset + 3] = byte.MaxValue;
                    }
                }
            }
            return new TextureAsset {
                Width = checked((ushort)width),
                Height = checked((ushort)height),
                Colors = rgba,
                PaletteColors = Array.Empty<byte>(),
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = mode == 3 ? TextureAssetAlphaPrecision.Opaque : TextureAssetAlphaPrecision.Binary
            };
        }

        /// <summary>Reads one tightly packed TIM section and accepts up to three declared alignment bytes.</summary>
        /// <param name="reader">Reader positioned at the section size.</param>
        /// <param name="width">Section width in 16-bit VRAM words.</param>
        /// <param name="height">Section height in rows.</param>
        /// <returns>Section payload without alignment bytes.</returns>
        static byte[] ReadSection(BinaryReader reader, out int width, out int height) {
            uint sectionLength = reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt16();
            width = reader.ReadUInt16();
            height = reader.ReadUInt16();
            long expectedBytes = (long)width * height * 2;
            if (width == 0 || height == 0 || width > 1024 || height > 512 || expectedBytes > MaximumSectionBytes
                || sectionLength < expectedBytes + 12 || sectionLength > expectedBytes + 15) {
                throw new InvalidDataException("TIM section dimensions and declared byte length disagree.");
            }
            byte[] data = reader.ReadBytes((int)expectedBytes);
            int paddingBytes = (int)(sectionLength - expectedBytes - 12);
            if (data.Length != expectedBytes || reader.ReadBytes(paddingBytes).Length != paddingBytes) {
                throw new EndOfStreamException("TIM section payload is truncated.");
            }
            return data;
        }

        /// <summary>Reads a little-endian BGR555 word from a validated section.</summary>
        /// <param name="bytes">Section payload.</param>
        /// <param name="offset">Word's byte offset.</param>
        /// <returns>Native PS1 color word.</returns>
        static ushort ReadWord(byte[] bytes, int offset) {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }
    }
}
