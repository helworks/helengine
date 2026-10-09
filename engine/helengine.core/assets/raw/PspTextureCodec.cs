namespace helengine {
    /// <summary>Encodes and validates GPU-ready PSP textures in a strict little-endian PGT1 payload.</summary>
    public static class PspTextureCodec {
        /// <summary>Identifies ASCII PGT1 independently of the enclosing asset serialization.</summary>
        public const uint Magic = 0x31544750;
        /// <summary>Gets the fixed header size; native texels begin on a 16-byte boundary.</summary>
        public const int HeaderLength = 48;
        /// <summary>Encodes a generic source; indexed modes require a prequantized Indexed4/Indexed8 source.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!PspTextureFormatCatalog.TryGetFormat(id, out PspTextureFormat format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Invalid PSP format or alpha policy.");
            PspTextureLayout layout = new PspTextureLayout(format, source.Width, source.Height);
            byte[] colors = new byte[HeaderLength + layout.TexelBytes];
            byte[] palette = format.IsIndexed ? new byte[layout.PaletteBytes] : null;
            Write(colors, 0, Magic, 4); Write(colors, 4, 1, 4); Write(colors, 8, (uint)format.Code, 4); Write(colors, 12, format.Swizzled ? 1u : 0u, 4);
            Write(colors, 16, (uint)source.Width, 4); Write(colors, 20, (uint)source.Height, 4); Write(colors, 24, (uint)layout.StorageWidth, 4); Write(colors, 28, (uint)layout.StorageHeight, 4);
            Write(colors, 32, (uint)layout.TexelBytes, 4); Write(colors, 36, (uint)format.PaletteCode, 4); Write(colors, 40, (uint)layout.PaletteEntries, 4); Write(colors, 44, (uint)layout.PaletteBytes, 4);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            if (format.IsCompressed) {
                XboxNativeTextureLayout dxt = DxtLayout(format.Code, layout.StorageWidth, layout.StorageHeight);
                byte[] standard = new byte[layout.TexelBytes];
                XboxNativeTextureDxtCodec.Encode(rgba, source.Width, source.Height, dxt, alpha, standard, 0);
                ConvertDxt(standard, 0, colors, HeaderLength, format.Code, layout.TexelBytes, true);
            } else {
                if (format.IsIndexed) {
                    if (source.ColorFormat != TextureAssetColorFormat.Indexed4 && source.ColorFormat != TextureAssetColorFormat.Indexed8) throw new ArgumentException("PSP indexed encoding requires a quantized source.");
                    int entries = source.PaletteColors.Length / 4;
                    if (entries > layout.PaletteEntries) throw new ArgumentException("PSP source palette exceeds the chosen index format.");
                    for (int entry = 0; entry < entries; entry++) Write(palette, entry * (format.PaletteCode == 3 ? 4 : 2), Pack(source.PaletteColors, entry * 4, format.PaletteCode, alpha), format.PaletteCode == 3 ? 4 : 2);
                }
                for (int y = 0; y < layout.StorageHeight; y++) {
                    for (int x = 0; x < layout.StorageWidth; x++) {
                        int sourcePixel = Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1);
                        uint value = format.IsIndexed ? (uint)(source.ColorFormat == TextureAssetColorFormat.Indexed4 ? (source.Colors[sourcePixel / 2] >> (sourcePixel % 2 * 4)) & 15 : source.Colors[sourcePixel]) : Pack(rgba, sourcePixel * 4, format.Code, alpha);
                        int offset = HeaderLength + layout.ByteOffset(x * format.BitsPerPixel / 8, y);
                        if (format.Code == 4) colors[offset] |= (byte)(value << (x % 2 * 4));
                        else Write(colors, offset, value, format.BitsPerPixel / 8);
                    }
                }
            }
            return new TextureAsset { Id = source.Id, RuntimeAssetId = source.RuntimeAssetId, AuthoringAssetId = source.AuthoringAssetId,
                FormerAuthoringAssetIds = CopyFormerAuthoringAssetIds(source.FormerAuthoringAssetIds), IsEngineOwned = source.IsEngineOwned,
                Width = source.Width, Height = source.Height, ColorFormat = TextureAssetColorFormat.PspNative, AlphaPrecision = alpha, Colors = colors, PaletteColors = palette };
        }
        /// <summary>Validates the full canonical header, payload and palette before publishing native metadata.</summary>
        [NativeOwnedReturn]
        public static PspTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.PspNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("A PSP native texture requires a complete PGT1 header.");
            byte[] data = asset.Colors;
            uint code = Read(data, 8, 4);
            uint flags = Read(data, 12, 4);
            uint paletteCode = Read(data, 36, 4);
            if (Read(data, 0, 4) != Magic || Read(data, 4, 4) != 1 || code > 10 || flags > 1 || paletteCode > 3 || (code >= 8 && flags != 0) || ((code < 4 || code >= 8) && paletteCode != 0)) throw new ArgumentException("Invalid PSP native version, format, flags or CLUT encoding.");
            PspTextureLayout layout = new PspTextureLayout(PspTextureFormatCatalog.GetFormat((int)code, flags != 0, (int)paletteCode), asset.Width, asset.Height);
            if (!layout.Format.SupportsAlpha(asset.AlphaPrecision) || Read(data, 16, 4) != asset.Width || Read(data, 20, 4) != asset.Height
                || Read(data, 24, 4) != (uint)layout.StorageWidth || Read(data, 28, 4) != (uint)layout.StorageHeight || Read(data, 32, 4) != (uint)layout.TexelBytes
                || Read(data, 40, 4) != (uint)layout.PaletteEntries || Read(data, 44, 4) != (uint)layout.PaletteBytes
                || data.Length != HeaderLength + layout.TexelBytes || (asset.PaletteColors == null ? 0 : asset.PaletteColors.Length) != layout.PaletteBytes) throw new ArgumentException("Malformed PSP texture extents, alpha, payload or palette length.");
            if (layout.Format.IsIndexed) {
                for (int y = 0; y < layout.StorageHeight; y++) {
                    for (int x = 0; x < layout.StorageWidth; x++) {
                        if (ReadIndex(asset.Colors, HeaderLength, layout, x, y) >= (uint)layout.PaletteEntries) throw new ArgumentException("PSP texture contains an out-of-range CLUT index.");
                    }
                }
            }
            return layout;
        }
        /// <summary>Decodes a validated payload into caller-owned RGBA preview pixels.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            PspTextureLayout layout = ReadLayout(asset);
            return DecodePixels(asset.Colors, HeaderLength, asset.PaletteColors, layout.Format.Code, layout.Format.PaletteCode,
                asset.Width, asset.Height, layout.Format.IsCompressed ? layout.Pitch * 4 : layout.Pitch, layout.StorageHeight, layout.Format.Swizzled);
        }
        /// <summary>Decodes bounded native rows for GIM import or a canonical PGT1 preview, without requantization.</summary>
        [NativeOwnedReturn]
        public static byte[] DecodePixels(byte[] pixels, int start, byte[] palette, int code, int paletteCode, int width, int height, int pitch, int storageHeight, bool swizzled) {
            if (code < 0 || code > 10 || paletteCode < 0 || paletteCode > 3 || width < 1 || height < 1 || width > 512 || height > 512 || pitch < 1 || storageHeight < height || storageHeight > 512 || start < 0) throw new ArgumentException("Invalid PSP pixel metadata.");
            PspTextureFormat format = PspTextureFormatCatalog.GetFormat(code, swizzled, code >= 4 && code <= 7 ? paletteCode : 0);
            int minPitch = code >= 8 ? (width + 3) / 4 * (code == 8 ? 8 : 16) : (width * format.BitsPerPixel + 7) / 8;
            int length = code >= 8 ? checked(pitch * ((storageHeight + 3) / 4)) : checked(pitch * storageHeight);
            if (pixels == null || pixels.Length - start < length || pitch < minPitch || pitch > 2048 || (swizzled && (code >= 8 || pitch % 16 != 0 || storageHeight % 8 != 0))) throw new ArgumentException("Truncated or invalid PSP pixel storage.");
            byte[] rgba = new byte[width * height * 4];
            if (code >= 8) {
                byte[] block = new byte[code == 8 ? 8 : 16];
                byte[] decoded = new byte[64];
                XboxNativeTextureLayout dxt = DxtLayout(code, 4, 4);
                for (int y = 0; y < height; y += 4) {
                    for (int x = 0; x < width; x += 4) {
                        ConvertDxt(pixels, start + y / 4 * pitch + x / 4 * block.Length, block, 0, code, block.Length, false);
                        XboxNativeTextureDxtCodec.Decode(block, 0, dxt, decoded);
                        for (int dy = 0; dy < 4 && y + dy < height; dy++) {
                            for (int dx = 0; dx < 4 && x + dx < width; dx++) {
                                for (int channel = 0; channel < 4; channel++) rgba[((y + dy) * width + x + dx) * 4 + channel] = decoded[(dy * 4 + dx) * 4 + channel];
                            }
                        }
                    }
                }
            } else {
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        int xByte = x * format.BitsPerPixel / 8;
                        int offset = start + (swizzled ? (y / 8 * (pitch / 16) + xByte / 16) * 128 + y % 8 * 16 + xByte % 16 : y * pitch + xByte);
                        uint value = code == 4 ? (uint)(pixels[offset] >> (x % 2 * 4) & 15) : Read(pixels, offset, format.BitsPerPixel / 8);
                        if (format.IsIndexed) {
                            int entry = (int)(value & (code == 4 ? 15u : 255u));
                            int bytes = paletteCode == 3 ? 4 : 2;
                            if (palette == null || palette.Length % bytes != 0 || entry >= palette.Length / bytes) throw new ArgumentException("PSP image index exceeds its palette.");
                            value = Read(palette, entry * bytes, bytes);
                        }
                        Unpack(value, format.IsIndexed ? paletteCode : code, rgba, (y * width + x) * 4);
                    }
                }
            }
            return rgba;
        }
        /// <summary>Copies identity history into caller-owned storage for managed and generated native assets.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerAuthoringAssetIds(string[] identities) {
            int length = identities == null ? 0 : identities.Length;
            string[] result = new string[length];
            for (int index = 0; index < length; index++) result[index] = identities[index];
            return result;
        }
        /// <summary>Reads a canonical index without applying CLUT mask aliases to invalid PGT1 data.</summary>
        static uint ReadIndex(byte[] data, int start, PspTextureLayout layout, int x, int y) {
            int offset = start + layout.ByteOffset(x * layout.Format.BitsPerPixel / 8, y);
            return layout.Format.Code == 4 ? (uint)(data[offset] >> (x % 2 * 4) & 15) : Read(data, offset, layout.Format.BitsPerPixel / 8);
        }
        /// <summary>Packs native direct-color words with red in the least significant channel.</summary>
        public static uint Pack(byte[] rgba, int offset, int code, TextureAssetAlphaPrecision alpha) {
            int r = rgba[offset], g = rgba[offset + 1], b = rgba[offset + 2];
            int a = XboxNativeTexturePixelCodec.QuantizeAlpha(rgba[offset + 3], alpha);
            if (code == 0) return (uint)((r * 31 + 127) / 255 | (g * 63 + 127) / 255 << 5 | (b * 31 + 127) / 255 << 11);
            if (code == 1) return (uint)((r * 31 + 127) / 255 | (g * 31 + 127) / 255 << 5 | (b * 31 + 127) / 255 << 10 | (a >= 128 ? 1 : 0) << 15);
            if (code == 2) return (uint)((r * 15 + 127) / 255 | (g * 15 + 127) / 255 << 4 | (b * 15 + 127) / 255 << 8 | (a * 15 + 127) / 255 << 12);
            return (uint)r | (uint)g << 8 | (uint)b << 16 | (uint)a << 24;
        }
        /// <summary>Expands native direct-color channels into straight RGBA.</summary>
        public static void Unpack(uint value, int code, byte[] rgba, int offset) {
            int rBits = code == 0 || code == 1 ? 5 : code == 2 ? 4 : 8;
            int gBits = code == 0 ? 6 : rBits;
            int bBits = rBits;
            rgba[offset] = Expand(value, rBits);
            rgba[offset + 1] = Expand(value >> rBits, gBits);
            rgba[offset + 2] = Expand(value >> (rBits + gBits), bBits);
            rgba[offset + 3] = code == 0 ? (byte)255 : code == 1 ? (byte)((value >> 15) * 255) : Expand(value >> (rBits + gBits + bBits), code == 2 ? 4 : 8);
        }
        /// <summary>Expands one quantized channel using the hardware's high-bit replication.</summary>
        static byte Expand(uint value, int bits) {
            uint mask = (1u << bits) - 1;
            uint channel = value & mask;
            return (byte)(bits == 5 ? channel << 3 | channel >> 2 : bits == 6 ? channel << 2 | channel >> 4 : bits == 4 ? channel * 17 : channel);
        }
        /// <summary>Reads a bounded little-endian native word.</summary>
        public static uint Read(byte[] data, int offset, int bytes) {
            if (data == null || offset < 0 || bytes < 1 || bytes > 4 || data.Length - offset < bytes) throw new ArgumentException("Truncated PSP word.");
            uint result = 0;
            for (int index = 0; index < bytes; index++) result |= (uint)data[offset + index] << (index * 8);
            return result;
        }
        /// <summary>Writes one little-endian native word.</summary>
        public static void Write(byte[] data, int offset, uint value, int bytes) {
            for (int index = 0; index < bytes; index++) data[offset + index] = (byte)(value >> (index * 8));
        }
        /// <summary>Creates the existing BC codec layout for one PSP DXT family.</summary>
        static XboxNativeTextureLayout DxtLayout(int code, int width, int height) {
            string id = code == 8 ? "Xbox.DXT1" : code == 9 ? "Xbox.DXT3" : "Xbox.DXT5";
            XboxNativeTextureFormatCatalog.TryGetFormat(id, out XboxNativeTextureFormatDefinition format);
            return new XboxNativeTextureLayout(format, width, height, 0);
        }
        /// <summary>Reorders BC blocks: PSP stores color selectors before endpoints and alpha after color.</summary>
        public static void ConvertDxt(byte[] input, int source, byte[] output, int target, int code, int length, bool toPsp) {
            int size = code == 8 ? 8 : 16;
            for (int block = 0; block < length; block += size) {
                for (int psp = 0; psp < size; psp++) {
                    int standard = psp < 4 ? (code == 8 ? 4 : 12) + psp : psp < 8 ? (code == 8 ? 0 : 8) + psp - 4
                        : code == 9 ? psp - 8 : psp < 12 ? psp - 4 : psp < 14 ? psp - 10 : psp - 14;
                    output[target + block + (toPsp ? psp : standard)] = input[source + block + (toPsp ? standard : psp)];
                }
            }
        }
    }
}
