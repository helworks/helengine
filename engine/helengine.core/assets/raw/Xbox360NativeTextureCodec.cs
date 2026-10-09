namespace helengine {
    /// <summary>Encodes and validates single-level linear Xenos texels in a strict little-endian X3T1 record.</summary>
    public static class Xbox360NativeTextureCodec {
        /// <summary>Identifies the ASCII bytes X3T1 independently of the outer HELE container byte order.</summary>
        public const uint Magic = 0x31543358;
        /// <summary>Gets the fourteen-word version-one header length.</summary>
        public const int HeaderLength = 56;
        /// <summary>Encodes any supported generic source into its actual GPU block or component representation.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string formatId, TextureAssetAlphaPrecision alphaPrecision) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!Xbox360NativeTextureFormatCatalog.TryGetFormat(formatId, out Xbox360NativeTextureFormatDefinition format) || !format.SupportsCooking) throw new ArgumentException("Xenos format has no verified texture codec.", nameof(formatId));
            if (!format.SupportsAlpha(alphaPrecision)) throw new ArgumentException("Alpha policy is incompatible with the selected Xenos storage.");
            Xbox360NativeTextureLayout layout = new Xbox360NativeTextureLayout(format, source.Width, source.Height);
            byte[] colors = new byte[checked(HeaderLength + layout.TexelLength)];
            WriteHeader(colors, layout);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            int code = Xbox360NativeTexturePixelCodec.BaseCode(format.HardwareFormat);
            if (code >= 18 && code <= 20) Xbox360NativeTextureDxtCodec.Encode(rgba, source.Width, source.Height, layout, alphaPrecision, colors, HeaderLength);
            else if (format.BlockHeight == 4) Xbox360NativeTextureBlockCodec.Encode(rgba, layout, alphaPrecision, colors, HeaderLength);
            else if (code == 11 || code == 12) EncodePairs(rgba, layout, colors);
            else {
                for (int y = 0; y < source.Height; y++) {
                    for (int x = 0; x < source.Width; x++) Xbox360NativeTexturePixelCodec.Encode(format.HardwareFormat, rgba, (y * source.Width + x) * 4, alphaPrecision, colors, HeaderLength + y * layout.PitchBytes + x * format.BytesPerBlock);
                }
            }
            return new TextureAsset {
                Id = source.Id, RuntimeAssetId = source.RuntimeAssetId, AuthoringAssetId = source.AuthoringAssetId,
                FormerAuthoringAssetIds = CopyFormerIds(source.FormerAuthoringAssetIds), IsEngineOwned = source.IsEngineOwned,
                Width = source.Width, Height = source.Height, ColorFormat = TextureAssetColorFormat.Xbox360Native,
                AlphaPrecision = alphaPrecision, Colors = colors, PaletteColors = null
            };
        }
        /// <summary>Returns new RGBA preview storage after strict header and payload validation.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            Xbox360NativeTextureLayout layout = ReadLayout(asset);
            byte[] result = new byte[checked(asset.Width * asset.Height * 4)];
            int code = Xbox360NativeTexturePixelCodec.BaseCode(layout.Format.HardwareFormat);
            if (code >= 18 && code <= 20) Xbox360NativeTextureDxtCodec.Decode(asset.Colors, HeaderLength, layout, result);
            else if (layout.Format.BlockHeight == 4) Xbox360NativeTextureBlockCodec.Decode(asset.Colors, HeaderLength, layout, result);
            else {
                for (int y = 0; y < asset.Height; y++) {
                    for (int x = 0; x < asset.Width; x++) {
                        int target = (y * asset.Width + x) * 4;
                        if (code == 11 || code == 12) {
                            int pair = HeaderLength + y * layout.PitchBytes + x / 2 * 4;
                            int red = asset.Colors[pair + (code == 11 ? 3 : 2)];
                            int blue = asset.Colors[pair + (code == 11 ? 1 : 0)];
                            int green = asset.Colors[pair + (code == 11 ? x % 2 * 2 : x % 2 * 2 + 1)];
                            XboxNativeTexturePixelCodec.SetPixel(result, target, red, green, blue, 255);
                        } else Xbox360NativeTexturePixelCodec.Decode(layout.Format.HardwareFormat, asset.Colors, HeaderLength + y * layout.PitchBytes + x * layout.Format.BytesPerBlock, result, target);
                    }
                }
            }
            return result;
        }
        /// <summary>Rejects noncanonical layout or fetch controls rather than interpreting arbitrary hardware data.</summary>
        [NativeOwnedReturn]
        public static Xbox360NativeTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (asset.ColorFormat != TextureAssetColorFormat.Xbox360Native || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("Texture is not a complete X3T1 record.");
            byte[] colors = asset.Colors;
            if (Read(colors, 0) != Magic || Read(colors, 4) != 1 || Read(colors, 12) != 0) throw new ArgumentException("Invalid X3T1 magic, version or flags.");
            uint code = Read(colors, 8);
            if (code > 63 || !Xbox360NativeTextureFormatCatalog.TryGetHardwareFormat((int)code, out Xbox360NativeTextureFormatDefinition format) || !format.SupportsCooking) throw new ArgumentException("Xenos format has no verified texture codec.");
            if (!format.SupportsAlpha(asset.AlphaPrecision)) throw new ArgumentException("Invalid native texture alpha policy.");
            Xbox360NativeTextureLayout layout = new Xbox360NativeTextureLayout(format, asset.Width, asset.Height);
            if (Read(colors, 16) != (uint)layout.RealWidth || Read(colors, 20) != (uint)layout.RealHeight ||
                Read(colors, 24) != (uint)layout.StorageWidth || Read(colors, 28) != (uint)layout.StorageHeight ||
                Read(colors, 32) != (uint)layout.PitchBytes || Read(colors, 36) != (uint)layout.TexelLength ||
                Read(colors, 40) != (uint)format.Swizzle || Read(colors, 44) != (uint)format.Signs ||
                Read(colors, 48) != (uint)format.NumberFormat || Read(colors, 52) != (uint)format.ExponentAdjust ||
                colors.Length != checked(HeaderLength + layout.TexelLength) || asset.PaletteColors != null && asset.PaletteColors.Length != 0) throw new ArgumentException("X3T1 layout, fetch controls or payload length is not canonical.");
            return layout;
        }
        /// <summary>Writes canonical little-endian layout and fetch control words.</summary>
        static void WriteHeader(byte[] colors, Xbox360NativeTextureLayout layout) {
            Write(colors, 0, Magic); Write(colors, 4, 1); Write(colors, 8, (uint)layout.Format.HardwareFormat); Write(colors, 12, 0);
            Write(colors, 16, (uint)layout.RealWidth); Write(colors, 20, (uint)layout.RealHeight); Write(colors, 24, (uint)layout.StorageWidth); Write(colors, 28, (uint)layout.StorageHeight);
            Write(colors, 32, (uint)layout.PitchBytes); Write(colors, 36, (uint)layout.TexelLength); Write(colors, 40, (uint)layout.Format.Swizzle);
            Write(colors, 44, (uint)layout.Format.Signs); Write(colors, 48, (uint)layout.Format.NumberFormat); Write(colors, 52, (uint)layout.Format.ExponentAdjust);
        }
        /// <summary>Writes a word without applying outer-container endian conversion.</summary>
        static void Write(byte[] colors, int offset, uint value) { XboxNativeTexturePixelCodec.WriteUInt32(colors, offset, value); }
        /// <summary>Reads a word without applying outer-container endian conversion.</summary>
        static uint Read(byte[] colors, int offset) { return XboxNativeTexturePixelCodec.ReadUInt32(colors, offset); }
        /// <summary>Preserves former authoring identities in new owned storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerIds(string[] input) {
            if (input == null) return new string[0];
            string[] result = new string[input.Length];
            for (int index = 0; index < input.Length; index++) result[index] = input[index];
            return result;
        }
        /// <summary>Encodes raw shared red/blue and independent green pairs; these formats perform no YUV color transform.</summary>
        static void EncodePairs(byte[] rgba, Xbox360NativeTextureLayout layout, byte[] output) {
            bool firstFormat = layout.Format.HardwareFormat == 11;
            for (int y = 0; y < layout.RealHeight; y++) {
                for (int x = 0; x < layout.RealWidth; x += 2) {
                    int first = (y * layout.RealWidth + x) * 4; int second = (y * layout.RealWidth + Math.Min(x + 1, layout.RealWidth - 1)) * 4;
                    int target = HeaderLength + y * layout.PitchBytes + x / 2 * 4;
                    output[target + (firstFormat ? 3 : 2)] = (byte)((rgba[first] + rgba[second] + 1) / 2);
                    output[target + (firstFormat ? 1 : 0)] = (byte)((rgba[first + 2] + rgba[second + 2] + 1) / 2);
                    output[target + (firstFormat ? 0 : 1)] = rgba[first + 1]; output[target + (firstFormat ? 2 : 3)] = rgba[second + 1];
                }
            }
        }
    }
}
