namespace helengine {
    /// <summary>Owns canonical DST1 native DS encoding, strict GPU-bound validation and RGBA preview decoding.</summary>
    public static class NintendoDsTextureCodec {
        /// <summary>Identifies the little-endian bytes DST1.</summary>
        public const uint Magic = 0x31545344;
        /// <summary>Gets the twelve-word descriptor size; compressed block descriptors follow native selectors.</summary>
        public const int HeaderLength = 48;
        /// <summary>Encodes pixels into a GPU format or native 2D tiles with independent owned storage and preserved identity.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!NintendoDsTextureFormatCatalog.TryGetFormat(id, out NintendoDsTextureFormat format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Unsupported Nintendo DS texture settings.");
            NintendoDsTextureLayout layout = new NintendoDsTextureLayout(format, source.Width, source.Height);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            byte[] texels = new byte[layout.TexelBytes + layout.DescriptorBytes];
            bool transparent = alpha == TextureAssetAlphaPrecision.Binary && format.PaletteEntries != 0;
            byte[] palette = format.PaletteEntries == 0 ? new byte[0] : NintendoDsTexturePalette.Build(rgba, format.PaletteEntries, transparent);
            if (format.Code == 5) palette = NintendoDsCompressedTextureCodec.Encode(rgba, layout, alpha, texels, 0);
            else {
                int[] cache = new int[32768];
                for (int index = 0; index < cache.Length; index++) cache[index] = -1;
                for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) {
                    int sourcePixel = (Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1)) * 4;
                    int color = NintendoDsTexturePalette.Color(rgba, sourcePixel), value = color;
                    int coverage = alpha == TextureAssetAlphaPrecision.Opaque ? 255 : rgba[sourcePixel + 3];
                    if (format.Code == 7) value |= coverage >= 128 ? 32768 : 0;
                    else {
                        if (cache[color] < 0) cache[color] = NintendoDsTexturePalette.Nearest(color, palette, transparent ? 1 : 0, format.PaletteEntries);
                        value = transparent && coverage < 128 ? 0 : cache[color];
                        if (format.Code == 1) value |= ((coverage * 7 + 127) / 255) << 5;
                        else if (format.Code == 6) value |= ((coverage * 31 + 127) / 255) << 3;
                    }
                    int pixel = NintendoDsTextureLayout.PixelIndex(x, y, layout.StorageWidth, format.IsTiled);
                    int offset = pixel * format.BitsPerPixel / 8;
                    if (format.BitsPerPixel < 8) texels[offset] |= (byte)(value << (pixel % (8 / format.BitsPerPixel) * format.BitsPerPixel));
                    else { texels[offset] = (byte)value; if (format.BitsPerPixel == 16) texels[offset + 1] = (byte)(value >> 8); }
                }
            }
            TextureAsset result = CreateNative(source.Width, source.Height, format.Code, alpha, transparent, texels, palette);
            result.Id = source.Id; result.RuntimeAssetId = source.RuntimeAssetId; result.AuthoringAssetId = source.AuthoringAssetId; result.IsEngineOwned = source.IsEngineOwned;
            result.FormerAuthoringAssetIds = CopyIds(source.FormerAuthoringAssetIds);
            return result;
        }
        /// <summary>Wraps borrowed native source storage in independently owned DST1 buffers, validating all native palette references.</summary>
        [NativeOwnedReturn]
        public static TextureAsset CreateNative(int width, int height, int code, TextureAssetAlphaPrecision alpha, bool colorZeroTransparent, [NativeNoEscape] byte[] texels, [NativeNoEscape] byte[] palette) {
            NintendoDsTextureLayout layout = new NintendoDsTextureLayout(NintendoDsTextureFormatCatalog.GetFormat(code), width, height);
            if (texels == null || palette == null || texels.Length != layout.TexelBytes + layout.DescriptorBytes) throw new ArgumentException("Invalid DS source storage lengths.");
            TextureAsset asset = new TextureAsset { Width = (ushort)width, Height = (ushort)height, ColorFormat = TextureAssetColorFormat.NintendoDsNative,
                AlphaPrecision = alpha, Colors = new byte[HeaderLength + texels.Length], PaletteColors = new byte[palette.Length] };
            for (int index = 0; index < texels.Length; index++) asset.Colors[HeaderLength + index] = texels[index];
            for (int index = 0; index < palette.Length; index++) asset.PaletteColors[index] = palette[index];
            Write(asset.Colors, 0, Magic); Write(asset.Colors, 4, 1); Write(asset.Colors, 8, (uint)code); Write(asset.Colors, 12, (uint)width); Write(asset.Colors, 16, (uint)height);
            Write(asset.Colors, 20, (uint)layout.StorageWidth); Write(asset.Colors, 24, (uint)layout.StorageHeight); Write(asset.Colors, 28, (uint)layout.TexelBytes);
            Write(asset.Colors, 32, (uint)layout.DescriptorBytes); Write(asset.Colors, 36, (uint)(palette.Length / 2)); Write(asset.Colors, 40, colorZeroTransparent ? 1u : 0u); Write(asset.Colors, 44, 0);
            NintendoDsTextureLayout validated = ReadLayout(asset);
            return asset;
        }
        /// <summary>Rejects invalid native descriptors, reserved fields, palette lengths and every padded texel's palette reference.</summary>
        [NativeOwnedReturn]
        public static NintendoDsTextureLayout ReadLayout([NativeNoEscape] TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.NintendoDsNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("A DS native texture requires a DST1 descriptor.");
            byte[] data = asset.Colors;
            uint code = Read(data, 8), flags = Read(data, 40), serializedEntries = Read(data, 36);
            if (Read(data, 0) != Magic || Read(data, 4) != 1 || code < 1 || code > 9 || flags > 1 || serializedEntries > 32768 || Read(data, 44) != 0) throw new ArgumentException("Invalid DS native descriptor.");
            int entries = (int)serializedEntries;
            NintendoDsTextureLayout layout = new NintendoDsTextureLayout(NintendoDsTextureFormatCatalog.GetFormat((int)code), asset.Width, asset.Height);
            int paletteBytes = asset.PaletteColors == null ? 0 : asset.PaletteColors.Length;
            if (!layout.Format.SupportsAlpha(asset.AlphaPrecision) || Read(data, 12) != asset.Width || Read(data, 16) != asset.Height
                || Read(data, 20) != (uint)layout.StorageWidth || Read(data, 24) != (uint)layout.StorageHeight || Read(data, 28) != (uint)layout.TexelBytes || Read(data, 32) != (uint)layout.DescriptorBytes
                || data.Length != HeaderLength + layout.TexelBytes + layout.DescriptorBytes || paletteBytes != entries * 2
                || (code == 7 ? entries != 0 : code == 5 ? entries < 2 : entries < 1 || entries > layout.Format.PaletteEntries)
                || (flags != 0 && !((code >= 2 && code <= 4) || code >= 8))) throw new ArgumentException("Malformed DS dimensions, alpha, palette or native buffer bounds.");
            if (code == 5) {
                for (int block = 0; block < layout.DescriptorBytes / 2; block++) {
                    int descriptor = Word(data, HeaderLength + layout.TexelBytes + block * 2), mode = descriptor >> 14;
                    int required = mode == 0 ? 3 : mode == 2 ? 4 : 2;
                    if ((descriptor & 16383) * 2 + required > entries) throw new ArgumentException("DS compressed block exceeds its palette.");
                }
            } else if (code != 7) {
                for (int pixel = 0; pixel < layout.StorageWidth * layout.StorageHeight; pixel++) {
                    int value = data[HeaderLength + pixel * layout.Format.BitsPerPixel / 8];
                    int index = code == 1 ? value & 31 : code == 6 ? value & 7 : layout.Format.BitsPerPixel == 8 ? value : value >> (pixel % (8 / layout.Format.BitsPerPixel) * layout.Format.BitsPerPixel) & ((1 << layout.Format.BitsPerPixel) - 1);
                    if (index >= entries) throw new ArgumentException("DS native index exceeds its palette.");
                }
            }
            return layout;
        }
        /// <summary>Decodes a validated native texture to caller-owned row-major RGBA pixels for previews or 2D rendering.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            NintendoDsTextureLayout layout = ReadLayout(asset);
            byte[] rgba = new byte[layout.Width * layout.Height * 4];
            bool transparent = Read(asset.Colors, 40) != 0;
            for (int y = 0; y < layout.Height; y++) for (int x = 0; x < layout.Width; x++) {
                uint color = Sample(asset.Colors, asset.PaletteColors, layout, transparent, x, y);
                int target = (y * layout.Width + x) * 4;
                for (int channel = 0; channel < 4; channel++) rgba[target + channel] = (byte)(color >> (channel * 8));
            }
            return rgba;
        }
        /// <summary>Samples a bounded component/index texture or a compressed block in logical coordinates.</summary>
        public static uint Sample([NativeNoEscape] byte[] colors, [NativeNoEscape] byte[] palette, [NativeNoEscape] NintendoDsTextureLayout layout, bool transparent, int x, int y) {
            int code = layout.Format.Code;
            if (code == 5) return NintendoDsCompressedTextureCodec.Sample(colors, HeaderLength, palette, layout, x, y);
            int pixel = NintendoDsTextureLayout.PixelIndex(x, y, layout.StorageWidth, layout.Format.IsTiled), bits = layout.Format.BitsPerPixel;
            int offset = HeaderLength + pixel * bits / 8, value = colors[offset];
            if (code == 7) { int word = Word(colors, offset); return Rgba(word, (word & 32768) != 0 ? 31 : 0); }
            int index = code == 1 ? value & 31 : code == 6 ? value & 7 : bits == 8 ? value : value >> (pixel % (8 / bits) * bits) & ((1 << bits) - 1);
            int alpha = code == 1 ? (value >> 5) * 4 + (value >> 6) : code == 6 ? value >> 3 : transparent && index == 0 ? 0 : 31;
            return Rgba(Word(palette, index * 2), alpha);
        }
        /// <summary>Expands native RGB555 and five-bit alpha into an RGBA word with red in the low byte.</summary>
        public static uint Rgba(int color, int alpha) => (uint)NintendoDsTexturePalette.Expand(color & 31) | (uint)NintendoDsTexturePalette.Expand(color >> 5 & 31) << 8 | (uint)NintendoDsTexturePalette.Expand(color >> 10 & 31) << 16 | (uint)NintendoDsTexturePalette.Expand(alpha) << 24;
        /// <summary>Reads a checked little-endian native palette or descriptor word.</summary>
        public static int Word(byte[] data, int offset) {
            if (data == null || offset < 0 || data.Length - offset < 2) throw new ArgumentException("Truncated DS native word.");
            return data[offset] | data[offset + 1] << 8;
        }
        /// <summary>Reads a checked little-endian DST1 field.</summary>
        public static uint Read(byte[] data, int offset) {
            if (data == null || offset < 0 || data.Length - offset < 4) throw new ArgumentException("Truncated DS native descriptor.");
            return (uint)data[offset] | (uint)data[offset + 1] << 8 | (uint)data[offset + 2] << 16 | (uint)data[offset + 3] << 24;
        }
        /// <summary>Writes one little-endian DST1 field.</summary>
        static void Write(byte[] data, int offset, uint value) { for (int index = 0; index < 4; index++) data[offset + index] = (byte)(value >> (index * 8)); }
        /// <summary>Copies identity history without sharing native owned storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyIds(string[] source) {
            string[] result = new string[source == null ? 0 : source.Length];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }
}
