namespace helengine.editor {
    /// <summary>Produces native Switch GPU texels with a portable decoded CPU cache and independent asset ownership.</summary>
    public static class SwitchNativeTextureCooker {
        /// <summary>Validates exact format/alpha selection and the authored resolution cap.</summary>
        public static void ValidateSettings(TextureAssetProcessorSettings settings) {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.MaxResolution < 0 || settings.MaxResolution > 16384
                || !SwitchTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out SwitchTextureFormat format) || !format.SupportsAlpha(settings.AlphaPrecision)) throw new ArgumentException("Unsupported Switch native texture settings.");
        }
        /// <summary>Resizes decoded source pixels, encodes native storage and caches the actual decoded output rather than the original source colors.</summary>
        public static TextureAsset Apply(TextureAsset source, TextureAssetProcessorSettings settings) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateSettings(settings); SwitchTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out SwitchTextureFormat format);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source); int width = source.Width, height = source.Height;
            if (settings.MaxResolution > 0 && Math.Max(width, height) > settings.MaxResolution) {
                double scale = settings.MaxResolution / (double)Math.Max(width, height);
                int resizedWidth = Math.Max(1, (int)Math.Round(width * scale)), resizedHeight = Math.Max(1, (int)Math.Round(height * scale));
                byte[] resized = new byte[SwitchTextureCodec.PreviewBytes(resizedWidth, resizedHeight)];
                for (int y = 0; y < resizedHeight; y++) for (int x = 0; x < resizedWidth; x++) {
                    int sx = Math.Clamp((int)Math.Round((x + 0.5d) * width / resizedWidth - 0.5d), 0, width - 1);
                    int sy = Math.Clamp((int)Math.Round((y + 0.5d) * height / resizedHeight - 0.5d), 0, height - 1);
                    Array.Copy(rgba, (sy * width + sx) * 4, resized, (y * resizedWidth + x) * 4, 4);
                }
                rgba = resized; width = resizedWidth; height = resizedHeight;
            }
            SwitchTextureCodec.PreviewBytes(width, height);
            for (int index = 3; index < rgba.Length; index += 4) rgba[index] = Quantize(rgba[index], settings.AlphaPrecision);
            SwitchTextureLayout layout = new SwitchTextureLayout(format, width, height);
            byte[] tight = format.BlockWidth > 1 ? SwitchTextureCompression.Encode(rgba, width, height, format) : SwitchTexturePixelCodec.Encode(rgba, width, height, format);
            byte[] texels = FromTight(tight, layout);
            byte[] preview = DecodeTight(tight, width, height, format, SwitchTextureCodec.IdentitySwizzle);
            TextureAsset result = SwitchTextureCodec.CreateNative(width, height, format.Code, format.BlockLinear, layout.BlockHeightLog2,
                settings.AlphaPrecision, SwitchTextureCodec.IdentitySwizzle, texels, preview);
            result.Id = source.Id; result.RuntimeAssetId = source.RuntimeAssetId; result.AuthoringAssetId = source.AuthoringAssetId;
            result.FormerAuthoringAssetIds = source.FormerAuthoringAssetIds == null ? Array.Empty<string>() : (string[])source.FormerAuthoringAssetIds.Clone(); result.IsEngineOwned = source.IsEngineOwned;
            return result;
        }
        /// <summary>Copies every byte into its native GOB or pitched address, retaining padded zero allocation bytes.</summary>
        public static byte[] FromTight(byte[] tight, SwitchTextureLayout layout) {
            int row = layout.Columns * layout.Format.BytesPerBlock;
            if (tight == null || tight.Length != row * layout.Rows) throw new InvalidDataException("Invalid tightly packed Switch storage.");
            byte[] result = new byte[layout.TexelBytes];
            for (int y = 0; y < layout.Rows; y++) for (int x = 0; x < row; x++) result[layout.ByteOffset(x, y)] = tight[y * row + x];
            return result;
        }
        /// <summary>Extracts native elements without decompressing them or changing their typed format.</summary>
        public static byte[] ToTight(byte[] native, SwitchTextureLayout layout) {
            if (native == null || native.Length != layout.TexelBytes) throw new InvalidDataException("Invalid native Switch storage length.");
            int row = layout.Columns * layout.Format.BytesPerBlock; byte[] result = new byte[row * layout.Rows];
            for (int y = 0; y < layout.Rows; y++) for (int x = 0; x < row; x++) result[y * row + x] = native[layout.ByteOffset(x, y)];
            return result;
        }
        /// <summary>Builds an authoritative decoded view and applies native component selection, including zero and one constants.</summary>
        public static byte[] DecodeTight(byte[] tight, int width, int height, SwitchTextureFormat format, uint swizzle) {
            byte[] rgba = format.BlockWidth > 1 ? SwitchTextureCompression.Decode(tight, width, height, format) : SwitchTexturePixelCodec.Decode(tight, width, height, format);
            byte[] selected = new byte[rgba.Length];
            for (int pixel = 0; pixel < rgba.Length; pixel += 4) for (int channel = 0; channel < 4; channel++) {
                int source = (int)(swizzle >> (channel * 8) & 255);
                if (source > 5) throw new InvalidDataException("Invalid native Switch component selector.");
                selected[pixel + channel] = source == 0 ? (byte)0 : source == 1 ? (byte)255 : rgba[pixel + source - 2];
            }
            return selected;
        }
        /// <summary>Converts source coverage to the exact selected alpha cook precision.</summary>
        static byte Quantize(byte value, TextureAssetAlphaPrecision alpha) {
            if (alpha == TextureAssetAlphaPrecision.Opaque) return 255;
            if (alpha == TextureAssetAlphaPrecision.Binary) return value < 128 ? (byte)0 : (byte)255;
            if (alpha == TextureAssetAlphaPrecision.A2) return (byte)(((value * 3 + 127) / 255) * 85);
            if (alpha == TextureAssetAlphaPrecision.A4) return (byte)(((value * 15 + 127) / 255) * 17);
            return value;
        }
    }
}
