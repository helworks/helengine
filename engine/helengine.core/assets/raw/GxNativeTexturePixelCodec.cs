namespace helengine {
    /// <summary>Packs GX native channels, big-endian words and tiled indices independently of descriptor ownership.</summary>
    public static class GxNativeTexturePixelCodec {
        /// <summary>Computes the row-major native tile start and texel position within its tile.</summary>
        public static int TileOffset(GxNativeTextureLayout layout, int x, int y) {
            GxNativeTextureFormatDefinition format = layout.Format;
            return ((y / format.BlockHeight) * (layout.StorageWidth / format.BlockWidth) + x / format.BlockWidth) * format.BytesPerBlock;
        }
        /// <summary>Computes the row-major texel index inside its native tile.</summary>
        public static int TilePixel(GxNativeTextureLayout layout, int x, int y) { return (y % layout.Format.BlockHeight) * layout.Format.BlockWidth + x % layout.Format.BlockWidth; }
        /// <summary>Packs one color into a native tile, retaining raw R, RG or RGB channels for depth-copy views.</summary>
        public static void EncodePixel(byte[] rgba, int source, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int payloadOffset, int x, int y) {
            int tile = payloadOffset + TileOffset(layout, x, y); int pixel = TilePixel(layout, x, y);
            int code = layout.Format.HardwareFormat; int native = layout.Format.NativeFormat;
            int intensity = native == 17 ? rgba[source] : Luma(rgba, source);
            int a = QuantizeAlpha(rgba[source + 3], alpha);
            if (code == 0) output[tile + pixel / 2] |= (byte)(((intensity * 15 + 127) / 255) << (pixel % 2 == 0 ? 4 : 0));
            else if (code == 1) output[tile + pixel] = (byte)intensity;
            else if (code == 2) output[tile + pixel] = (byte)(((a * 15 + 127) / 255 << 4) | (intensity * 15 + 127) / 255);
            else if (code == 3) {
                output[tile + pixel * 2] = native == 19 ? rgba[source] : (byte)a;
                output[tile + pixel * 2 + 1] = native == 19 ? rgba[source + 1] : (byte)intensity;
            } else if (code == 4 || code == 5) WriteBigWord(output, tile + pixel * 2, code == 4 ? PackRgb565(rgba[source], rgba[source + 1], rgba[source + 2]) : PackRgb5A3(rgba[source], rgba[source + 1], rgba[source + 2], a));
            else if (code == 6) {
                output[tile + pixel * 2] = native == 22 ? (byte)255 : (byte)a; output[tile + pixel * 2 + 1] = rgba[source];
                output[tile + 32 + pixel * 2] = rgba[source + 1]; output[tile + 33 + pixel * 2] = rgba[source + 2];
            } else throw new ArgumentException("GX direct pixel encoder requires a direct uncompressed sampler.");
        }
        /// <summary>Decodes native sampled channels, including intensity-coupled alpha and planar RGBA8.</summary>
        public static void DecodePixel(byte[] input, int payloadOffset, GxNativeTextureLayout layout, int x, int y, byte[] rgba, int target) {
            int tile = payloadOffset + TileOffset(layout, x, y); int pixel = TilePixel(layout, x, y); int code = layout.Format.HardwareFormat;
            if (code == 0) { int value = ((input[tile + pixel / 2] >> (pixel % 2 == 0 ? 4 : 0)) & 15) * 17; SetPixel(rgba, target, value, value, value, value); }
            else if (code == 1) { int value = input[tile + pixel]; SetPixel(rgba, target, value, value, value, value); }
            else if (code == 2) { int value = (input[tile + pixel] & 15) * 17; SetPixel(rgba, target, value, value, value, (input[tile + pixel] >> 4) * 17); }
            else if (code == 3) DecodeColor(input, tile + pixel * 2, 0, rgba, target);
            else if (code == 4 || code == 5) DecodeColor(input, tile + pixel * 2, code == 4 ? 1 : 2, rgba, target);
            else if (code == 6) SetPixel(rgba, target, input[tile + pixel * 2 + 1], input[tile + 32 + pixel * 2], input[tile + 33 + pixel * 2], input[tile + pixel * 2]);
            else throw new ArgumentException("GX direct pixel decoder requires a direct uncompressed sampler.");
        }
        /// <summary>Writes one index in native high-nibble, byte or big-endian fourteen-bit storage.</summary>
        public static void WriteIndex(byte[] output, int payloadOffset, GxNativeTextureLayout layout, int x, int y, int index) {
            int tile = payloadOffset + TileOffset(layout, x, y); int pixel = TilePixel(layout, x, y);
            if (layout.Format.IndexBitDepth == 4) output[tile + pixel / 2] |= (byte)(index << (pixel % 2 == 0 ? 4 : 0));
            else if (layout.Format.IndexBitDepth == 8) output[tile + pixel] = (byte)index;
            else WriteBigWord(output, tile + pixel * 2, index);
        }
        /// <summary>Reads native indices without masking malformed CI14 reserved bits.</summary>
        public static int ReadIndex(byte[] input, int payloadOffset, GxNativeTextureLayout layout, int x, int y) {
            int tile = payloadOffset + TileOffset(layout, x, y); int pixel = TilePixel(layout, x, y);
            return layout.Format.IndexBitDepth == 4 ? (input[tile + pixel / 2] >> (pixel % 2 == 0 ? 4 : 0)) & 15 : layout.Format.IndexBitDepth == 8 ? input[tile + pixel] : ReadBigWord(input, tile + pixel * 2);
        }
        /// <summary>Packs a TLUT entry; IA8 stores alpha first followed by intensity.</summary>
        public static void EncodeColor(byte[] rgba, int source, int tlut, TextureAssetAlphaPrecision alpha, byte[] output, int target) {
            int a = QuantizeAlpha(rgba[source + 3], alpha);
            if (tlut == 0) { output[target] = (byte)a; output[target + 1] = (byte)Luma(rgba, source); }
            else WriteBigWord(output, target, tlut == 1 ? PackRgb565(rgba[source], rgba[source + 1], rgba[source + 2]) : PackRgb5A3(rgba[source], rgba[source + 1], rgba[source + 2], a));
        }
        /// <summary>Expands native TLUT or direct color bits exactly as the GX sampler does.</summary>
        public static void DecodeColor(byte[] input, int source, int tlut, byte[] rgba, int target) {
            if (tlut == 0) { int value = input[source + 1]; SetPixel(rgba, target, value, value, value, input[source]); return; }
            int word = ReadBigWord(input, source);
            if (tlut == 1) SetPixel(rgba, target, Expand5(word >> 11), Expand6(word >> 5), Expand5(word), 255);
            else if ((word & 32768) != 0) SetPixel(rgba, target, Expand5(word >> 10), Expand5(word >> 5), Expand5(word), 255);
            else { int a = (word >> 12) & 7; SetPixel(rgba, target, ((word >> 8) & 15) * 17, ((word >> 4) & 15) * 17, (word & 15) * 17, (a << 5) | (a << 2) | (a >> 1)); }
        }
        /// <summary>Quantizes source coverage into the requested GX alpha precision.</summary>
        public static int QuantizeAlpha(int value, TextureAssetAlphaPrecision alpha) {
            if (alpha == TextureAssetAlphaPrecision.Opaque) return 255;
            if (alpha == TextureAssetAlphaPrecision.Binary) return value >= 128 ? 255 : 0;
            if (alpha == TextureAssetAlphaPrecision.A4) return ((value * 15 + 127) / 255) * 17;
            if (alpha == TextureAssetAlphaPrecision.A3) { int a = (value * 7 + 127) / 255; return (a << 5) | (a << 2) | (a >> 1); }
            return value;
        }
        /// <summary>Packs nearest RGB565 channel values into a native word.</summary>
        public static int PackRgb565(int red, int green, int blue) { return ((red * 31 + 127) / 255 << 11) | ((green * 63 + 127) / 255 << 5) | (blue * 31 + 127) / 255; }
        /// <summary>Packs true three-bit coverage, using the opaque five-bit RGB branch only for full alpha.</summary>
        static int PackRgb5A3(int red, int green, int blue, int alpha) {
            if (alpha == 255) return 32768 | ((red * 31 + 127) / 255 << 10) | ((green * 31 + 127) / 255 << 5) | (blue * 31 + 127) / 255;
            return ((alpha * 7 + 127) / 255 << 12) | ((red * 15 + 127) / 255 << 8) | ((green * 15 + 127) / 255 << 4) | (blue * 15 + 127) / 255;
        }
        /// <summary>Computes integer Rec.601 luma for intensity-only selections.</summary>
        static int Luma(byte[] rgba, int offset) { return (rgba[offset] * 77 + rgba[offset + 1] * 150 + rgba[offset + 2] * 29 + 128) >> 8; }
        /// <summary>Expands five bits with hardware bit replication.</summary>
        public static int Expand5(int value) { value &= 31; return (value << 3) | (value >> 2); }
        /// <summary>Expands six bits with hardware bit replication.</summary>
        public static int Expand6(int value) { value &= 63; return (value << 2) | (value >> 4); }
        /// <summary>Writes a native big-endian sixteen-bit GPU word.</summary>
        public static void WriteBigWord(byte[] output, int offset, int value) { output[offset] = (byte)(value >> 8); output[offset + 1] = (byte)value; }
        /// <summary>Reads a native big-endian sixteen-bit GPU word.</summary>
        public static int ReadBigWord(byte[] input, int offset) { return input[offset] << 8 | input[offset + 1]; }
        /// <summary>Writes decoded sampled RGBA channels into borrowed output storage.</summary>
        public static void SetPixel(byte[] rgba, int offset, int red, int green, int blue, int alpha) { rgba[offset] = (byte)red; rgba[offset + 1] = (byte)green; rgba[offset + 2] = (byte)blue; rgba[offset + 3] = (byte)alpha; }
    }
}
