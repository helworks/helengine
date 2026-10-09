namespace helengine {
    /// <summary>Encodes compact GS host-transfer streams and CSM1 palettes; the GS performs its own local-memory addressing.</summary>
    public static class Ps2GsTexturePixelCodec {
        /// <summary>Encodes direct texels or already-quantized indices without expanding indexed GPU storage.</summary>
        [NativeOwnedReturn]
        public static byte[] EncodePixels(TextureAsset source, string formatId, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Ps2GsTextureFormatDefinition format = GetFormat(formatId, alpha);
            int byteLength = format.GetPixelByteLength(source.Width, source.Height);
            if (format.IsIndexed) return EncodeIndexedPixels(source, format, byteLength);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            try {
                return EncodeDirectPixels(rgba, format, alpha, byteLength);
            } finally {
                NativeOwnership.Release(ref rgba);
            }
        }
        /// <summary>Encodes a complete padded CLUT with identity order for sixteen entries and the GS CSM1 bit-three/four permutation for 256 entries.</summary>
        [NativeOwnedReturn]
        public static byte[] EncodePalette(TextureAsset source, string formatId, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Ps2GsTextureFormatDefinition format = GetFormat(formatId, alpha);
            if (!format.IsIndexed) return new byte[0];
            int entries = ValidateIndexedSource(source, format);
            int entryBytes = format.ClutFormatCode == 0 ? 4 : 2;
            byte[] palette = new byte[format.PaletteByteLength];
            for (int index = 0; index < entries; index++) {
                EncodeColor(source.PaletteColors, index * 4, format.ClutFormatCode, alpha, palette, PaletteTransferIndex(index, format.PaletteEntryCount) * entryBytes);
            }
            return palette;
        }
        /// <summary>Decodes canonical payloads with the selected format's default alpha policy.</summary>
        [NativeOwnedReturn]
        public static byte[] DecodeToRgba32(byte[] pixels, byte[] palette, int width, int height, string formatId) {
            if (!Ps2GsTextureFormatCatalog.TryGetFormat(formatId, out Ps2GsTextureFormatDefinition format)) throw new ArgumentException("Unknown PS2 GS texture format.");
            return DecodeToRgba32(pixels, palette, width, height, formatId, format.DefaultAlphaPrecision);
        }
        /// <summary>Decodes native sampled channels using GS alpha zero-through-128 and TEXA's binary or opaque sixteen-bit policy.</summary>
        [NativeOwnedReturn]
        public static byte[] DecodeToRgba32(byte[] pixels, byte[] palette, int width, int height, string formatId, TextureAssetAlphaPrecision alpha) {
            Ps2GsTextureFormatDefinition format = GetFormat(formatId, alpha);
            if (pixels == null || pixels.Length != format.GetPixelByteLength(width, height)) throw new ArgumentException("Invalid compact GS transfer length.");
            if (format.IsIndexed && (palette == null || palette.Length != format.PaletteByteLength)) throw new ArgumentException("Invalid complete GS CLUT length.");
            if (!format.IsIndexed && palette != null && palette.Length != 0) throw new ArgumentException("Direct GS textures cannot contain a CLUT.");
            byte[] rgba = new byte[checked(width * height * 4)];
            for (int pixel = 0; pixel < width * height; pixel++) {
                if (format.IsIndexed) {
                    int index = format.IndexBitDepth == 8 ? pixels[pixel] : (pixels[pixel / 2] >> (pixel % 2 * 4)) & 15;
                    int entryBytes = format.ClutFormatCode == 0 ? 4 : 2;
                    DecodeColor(palette, PaletteTransferIndex(index, format.PaletteEntryCount) * entryBytes, format.ClutFormatCode, alpha, rgba, pixel * 4);
                } else DecodeColor(pixels, pixel * format.BitsPerPixel / 8, format.FormatCode, alpha, rgba, pixel * 4);
            }
            return rgba;
        }
        /// <summary>Validates all authored indices before allocating a compact native index stream with unambiguous return ownership.</summary>
        [NativeOwnedReturn]
        static byte[] EncodeIndexedPixels(TextureAsset source, Ps2GsTextureFormatDefinition format, int byteLength) {
            int count = source.Width * source.Height;
            int paletteEntries = ValidateIndexedSource(source, format);
            for (int pixel = 0; pixel < count; pixel++) if (ReadSourceIndex(source, pixel) >= paletteEntries) throw new ArgumentException("Texture index exceeds the authored palette.");
            byte[] pixels = new byte[byteLength];
            for (int pixel = 0; pixel < count; pixel++) {
                int index = ReadSourceIndex(source, pixel);
                if (format.IndexBitDepth == 8) pixels[pixel] = (byte)index;
                else pixels[pixel / 2] |= (byte)(index << (pixel % 2 * 4));
            }
            return pixels;
        }
        /// <summary>Packs an already-validated borrowed RGBA buffer into an independent direct-color transfer stream.</summary>
        [NativeOwnedReturn]
        static byte[] EncodeDirectPixels(byte[] rgba, Ps2GsTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, int byteLength) {
            byte[] pixels = new byte[byteLength];
            for (int pixel = 0; pixel < rgba.Length / 4; pixel++) EncodeColor(rgba, pixel * 4, format.FormatCode, alpha, pixels, pixel * format.BitsPerPixel / 8);
            return pixels;
        }
        /// <summary>Finds a borrowed catalog description and rejects precision outside its real storage capacity.</summary>
        [NativeBorrowedReturn]
        static Ps2GsTextureFormatDefinition GetFormat(string id, TextureAssetAlphaPrecision alpha) {
            if (!Ps2GsTextureFormatCatalog.TryGetFormat(id, out Ps2GsTextureFormatDefinition format)) throw new ArgumentException("Unknown PS2 GS texture format.");
            if (!format.SupportsAlpha(alpha)) throw new ArgumentException("Alpha policy is incompatible with the selected GS texture or CLUT.");
            return format;
        }
        /// <summary>Checks generic index storage, palette bounds and continuous byte length before any native conversion.</summary>
        static int ValidateIndexedSource(TextureAsset source, Ps2GsTextureFormatDefinition format) {
            if (source.ColorFormat != TextureAssetColorFormat.Indexed4 && source.ColorFormat != TextureAssetColorFormat.Indexed8) throw new ArgumentException("GS indexed formats require an already-quantized indexed source.");
            if (!TextureAssetPixelCodec.IsAlphaPrecisionSupported(source.ColorFormat, source.AlphaPrecision)) throw new ArgumentException("Invalid generic indexed source alpha policy.");
            if (source.Colors == null || source.Colors.Length != TextureAssetPixelCodec.GetPixelByteLength(source.ColorFormat, source.Width, source.Height)) throw new ArgumentException("Invalid generic index buffer length.");
            if (source.PaletteColors == null || source.PaletteColors.Length == 0 || source.PaletteColors.Length % 4 != 0 || source.PaletteColors.Length / 4 > format.PaletteEntryCount || source.PaletteColors.Length / 4 > (source.ColorFormat == TextureAssetColorFormat.Indexed4 ? 16 : 256)) throw new ArgumentException("Authored palette exceeds GS or generic index capacity.");
            return source.PaletteColors.Length / 4;
        }
        /// <summary>Reads the generic engine's continuous low-nibble-first index representation.</summary>
        static int ReadSourceIndex(TextureAsset source, int pixel) { return source.ColorFormat == TextureAssetColorFormat.Indexed8 ? source.Colors[pixel] : (source.Colors[pixel / 2] >> (pixel % 2 * 4)) & 15; }
        /// <summary>Maps logical entries into host transfer coordinates valid for CT32, CT16 and CT16S CSM1 CLUTs.</summary>
        static int PaletteTransferIndex(int index, int count) { return count == 16 ? index : (index & ~24) | ((index & 8) << 1) | ((index & 16) >> 1); }
        /// <summary>Encodes native GS color channel bits; depth PSMs use the same raw sampled color word without a depth-to-luma conversion.</summary>
        static void EncodeColor(byte[] rgba, int source, int code, TextureAssetAlphaPrecision alpha, byte[] output, int target) {
            int alphaValue = alpha == TextureAssetAlphaPrecision.Opaque ? 255 : alpha == TextureAssetAlphaPrecision.Binary ? rgba[source + 3] >= 128 ? 255 : 0 : alpha == TextureAssetAlphaPrecision.A4 ? (rgba[source + 3] >> 4) * 17 : rgba[source + 3];
            if (code == 2 || code == 10 || code == 50 || code == 58) {
                int word = (rgba[source] >> 3) | ((rgba[source + 1] >> 3) << 5) | ((rgba[source + 2] >> 3) << 10) | (alphaValue >= 128 ? 0x8000 : 0);
                output[target] = (byte)word; output[target + 1] = (byte)(word >> 8);
            } else {
                output[target] = rgba[source]; output[target + 1] = rgba[source + 1]; output[target + 2] = rgba[source + 2];
                if (code != 1 && code != 49) output[target + 3] = (byte)((alphaValue * 128 + 127) / 255);
            }
        }
        /// <summary>Decodes GS RGB5 by shifting into eight-bit channels and maps native blending alpha back to preview coverage.</summary>
        static void DecodeColor(byte[] input, int source, int code, TextureAssetAlphaPrecision alpha, byte[] rgba, int target) {
            if (code == 2 || code == 10 || code == 50 || code == 58) {
                int word = input[source] | input[source + 1] << 8;
                rgba[target] = (byte)((word & 31) << 3); rgba[target + 1] = (byte)(((word >> 5) & 31) << 3); rgba[target + 2] = (byte)(((word >> 10) & 31) << 3);
                rgba[target + 3] = alpha == TextureAssetAlphaPrecision.Opaque || (word & 0x8000) != 0 ? (byte)255 : (byte)0;
            } else {
                rgba[target] = input[source]; rgba[target + 1] = input[source + 1]; rgba[target + 2] = input[source + 2];
                rgba[target + 3] = code == 1 || code == 49 || alpha == TextureAssetAlphaPrecision.Opaque ? (byte)255 : (byte)Math.Min(255, (input[source + 3] * 255 + 64) / 128);
            }
        }
    }
}
