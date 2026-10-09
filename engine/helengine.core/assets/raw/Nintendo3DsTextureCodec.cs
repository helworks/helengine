namespace helengine {
    /// <summary>Cooks and validates N3T1 payloads containing GPU-ready PICA200 tiles without runtime conversion.</summary>
    public static class Nintendo3DsTextureCodec {
        /// <summary>Identifies the little-endian ASCII N3T1 native payload.</summary>
        public const uint Magic = 0x3154334e;
        /// <summary>Gets the descriptor length; GPU texels follow at an aligned offset.</summary>
        public const int HeaderLength = 32;
        /// <summary>Encodes a supported source to native tiles, preserving identity and applying the requested alpha policy.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!Nintendo3DsTextureFormatCatalog.TryGetFormat(id, out Nintendo3DsTextureFormat format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Unsupported 3DS format or alpha precision.");
            Nintendo3DsTextureLayout layout = new Nintendo3DsTextureLayout(format, source.Width, source.Height);
            byte[] output = new byte[HeaderLength + layout.TexelBytes];
            Write(output, 0, Magic); Write(output, 4, 1); Write(output, 8, (uint)format.Code);
            Write(output, 12, source.Width); Write(output, 16, source.Height); Write(output, 20, (uint)layout.StorageWidth); Write(output, 24, (uint)layout.StorageHeight); Write(output, 28, (uint)layout.TexelBytes);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            if (format.IsCompressed) Nintendo3DsEtcTextureCodec.Encode(rgba, source.Width, source.Height, layout, alpha, output, HeaderLength);
            else {
                for (int y = 0; y < layout.StorageHeight; y++) {
                    for (int x = 0; x < layout.StorageWidth; x++) {
                        int sourceOffset = (Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1)) * 4;
                        uint word = Nintendo3DsTexturePixelCodec.Pack(rgba, sourceOffset, format.Code, alpha);
                        int pixel = Nintendo3DsTextureLayout.PixelIndex(x, y, layout.StorageWidth);
                        int offset = HeaderLength + pixel * format.BitsPerPixel / 8;
                        if (format.BitsPerPixel == 4) output[offset] |= (byte)(word << (pixel % 2 * 4));
                        else for (int index = 0; index < format.BitsPerPixel / 8; index++) output[offset + index] = (byte)(word >> (index * 8));
                    }
                }
            }
            return new TextureAsset { Id = source.Id, RuntimeAssetId = source.RuntimeAssetId, AuthoringAssetId = source.AuthoringAssetId, FormerAuthoringAssetIds = CopyIdentities(source.FormerAuthoringAssetIds),
                IsEngineOwned = source.IsEngineOwned, Width = source.Width, Height = source.Height, ColorFormat = TextureAssetColorFormat.Nintendo3DsNative, AlphaPrecision = alpha, Colors = output };
        }
        /// <summary>Validates every descriptor word and payload bound before publishing a native layout.</summary>
        [NativeOwnedReturn]
        public static Nintendo3DsTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.Nintendo3DsNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("A 3DS native texture requires an N3T1 descriptor.");
            byte[] data = asset.Colors;
            uint code = Read(data, 8);
            if (Read(data, 0) != Magic || Read(data, 4) != 1 || code > 13) throw new ArgumentException("Invalid 3DS native version or encoding.");
            Nintendo3DsTextureLayout layout = new Nintendo3DsTextureLayout(Nintendo3DsTextureFormatCatalog.GetFormat((int)code), asset.Width, asset.Height);
            if (!layout.Format.SupportsAlpha(asset.AlphaPrecision) || Read(data, 12) != asset.Width || Read(data, 16) != asset.Height
                || Read(data, 20) != (uint)layout.StorageWidth || Read(data, 24) != (uint)layout.StorageHeight || Read(data, 28) != (uint)layout.TexelBytes
                || data.Length != HeaderLength + layout.TexelBytes || (asset.PaletteColors != null && asset.PaletteColors.Length != 0)) throw new ArgumentException("Malformed 3DS native dimensions, palette, alpha or payload.");
            return layout;
        }
        /// <summary>Expands a checked native texture into caller-owned RGBA preview pixels.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            Nintendo3DsTextureLayout layout = ReadLayout(asset);
            return DecodePixels(asset.Colors, HeaderLength, layout.Format.Code, asset.Width, asset.Height, layout.StorageWidth, layout.StorageHeight);
        }
        /// <summary>Decodes native base-level tiles for a canonical payload or tex3ds container.</summary>
        [NativeOwnedReturn]
        public static byte[] DecodePixels(byte[] data, int start, int code, int width, int height, int storageWidth, int storageHeight) {
            Nintendo3DsTextureFormat format = Nintendo3DsTextureFormatCatalog.GetFormat(code);
            if (width < 1 || height < 1 || width > storageWidth || height > storageHeight || storageWidth < 8 || storageHeight < 8 || storageWidth > 1024 || storageHeight > 1024
                || (storageWidth & (storageWidth - 1)) != 0 || (storageHeight & (storageHeight - 1)) != 0 || start < 0 || data == null
                || data.Length - start < storageWidth * storageHeight * format.BitsPerPixel / 8) throw new ArgumentException("Invalid or truncated 3DS native tile storage.");
            byte[] rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int target = (y * width + x) * 4;
                    if (format.IsCompressed) Nintendo3DsEtcTextureCodec.Sample(data, start + Nintendo3DsTextureLayout.BlockIndex(x, y, storageWidth) * (code == 12 ? 8 : 16), code, x % 4, y % 4, rgba, target);
                    else {
                        int pixel = Nintendo3DsTextureLayout.PixelIndex(x, y, storageWidth);
                        int offset = start + pixel * format.BitsPerPixel / 8;
                        uint value = 0;
                        if (format.BitsPerPixel == 4) value = (uint)(data[offset] >> (pixel % 2 * 4) & 15);
                        else for (int index = 0; index < format.BitsPerPixel / 8; index++) value |= (uint)data[offset + index] << (index * 8);
                        Nintendo3DsTexturePixelCodec.Unpack(value, code, rgba, target);
                    }
                }
            }
            return rgba;
        }
        /// <summary>Copies asset identity history into separately owned storage for native code generation.</summary>
        [NativeOwnedReturn]
        static string[] CopyIdentities(string[] source) {
            int count = source == null ? 0 : source.Length;
            string[] result = new string[count];
            for (int index = 0; index < count; index++) result[index] = source[index];
            return result;
        }
        /// <summary>Reads a bounded little-endian descriptor word.</summary>
        public static uint Read(byte[] data, int offset) {
            if (data == null || offset < 0 || data.Length - offset < 4) throw new ArgumentException("Truncated 3DS descriptor word.");
            return (uint)data[offset] | (uint)data[offset + 1] << 8 | (uint)data[offset + 2] << 16 | (uint)data[offset + 3] << 24;
        }
        /// <summary>Writes one little-endian descriptor word.</summary>
        static void Write(byte[] data, int offset, uint value) {
            for (int index = 0; index < 4; index++) data[offset + index] = (byte)(value >> (index * 8));
        }
    }
}
