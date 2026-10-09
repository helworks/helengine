namespace helengine {
    /// <summary>Encodes four BC1-like subblocks per GX tile and decodes GX-specific three-eighths interpolation.</summary>
    public static class GxNativeTextureCmprCodec {
        /// <summary>Encodes clamped complete native CMPR tiles without allocating temporary endpoint tables.</summary>
        public static void Encode(byte[] rgba, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int payloadOffset) {
            for (int y = 0; y < layout.StorageHeight; y += 8) {
                for (int x = 0; x < layout.StorageWidth; x += 8) {
                    int tile = payloadOffset + GxNativeTexturePixelCodec.TileOffset(layout, x, y);
                    for (int block = 0; block < 4; block++) EncodeBlock(rgba, layout, alpha, x + block % 2 * 4, y + block / 2 * 4, output, tile + block * 8);
                }
            }
        }
        /// <summary>Decodes logical pixels from tiled CMPR storage using the actual GX interpolator.</summary>
        public static void Decode(byte[] input, int payloadOffset, GxNativeTextureLayout layout, byte[] rgba) {
            for (int y = 0; y < layout.RealHeight; y++) {
                for (int x = 0; x < layout.RealWidth; x++) {
                    int block = (y % 8 / 4) * 2 + x % 8 / 4;
                    int offset = payloadOffset + GxNativeTexturePixelCodec.TileOffset(layout, x, y) + block * 8;
                    int first = GxNativeTexturePixelCodec.ReadBigWord(input, offset); int second = GxNativeTexturePixelCodec.ReadBigWord(input, offset + 2);
                    int index = (input[offset + 4 + y % 4] >> (6 - x % 4 * 2)) & 3;
                    GxNativeTexturePixelCodec.SetPixel(rgba, (y * layout.RealWidth + x) * 4, Channel(first, second, index, 0), Channel(first, second, index, 1), Channel(first, second, index, 2), first <= second && index == 3 ? 0 : 255);
                }
            }
        }
        /// <summary>Selects the farthest authored endpoint pair and nearest representable colors for one four-by-four subblock.</summary>
        static void EncodeBlock(byte[] rgba, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, int x, int y, byte[] output, int target) {
            bool transparent = false; int firstPixel = -1; int secondPixel = -1; int distance = -1;
            for (int a = 0; a < 16; a++) {
                int sourceA = SourceOffset(layout, x + a % 4, y + a / 4);
                if (alpha == TextureAssetAlphaPrecision.Binary && rgba[sourceA + 3] < 128) { transparent = true; continue; }
                if (firstPixel < 0) { firstPixel = sourceA; secondPixel = sourceA; }
                for (int b = a + 1; b < 16; b++) {
                    int sourceB = SourceOffset(layout, x + b % 4, y + b / 4);
                    if (alpha == TextureAssetAlphaPrecision.Binary && rgba[sourceB + 3] < 128) continue;
                    int red = rgba[sourceA] - rgba[sourceB]; int green = rgba[sourceA + 1] - rgba[sourceB + 1]; int blue = rgba[sourceA + 2] - rgba[sourceB + 2];
                    int candidate = red * red + green * green + blue * blue;
                    if (candidate > distance) { distance = candidate; firstPixel = sourceA; secondPixel = sourceB; }
                }
            }
            int first = firstPixel < 0 ? 0 : GxNativeTexturePixelCodec.PackRgb565(rgba[firstPixel], rgba[firstPixel + 1], rgba[firstPixel + 2]);
            int second = secondPixel < 0 ? 0 : GxNativeTexturePixelCodec.PackRgb565(rgba[secondPixel], rgba[secondPixel + 1], rgba[secondPixel + 2]);
            if (transparent && first > second || !transparent && first < second) { int swap = first; first = second; second = swap; }
            if (!transparent && first == second) { if (first == 0) first = 1; else second--; }
            GxNativeTexturePixelCodec.WriteBigWord(output, target, first); GxNativeTexturePixelCodec.WriteBigWord(output, target + 2, second);
            for (int pixel = 0; pixel < 16; pixel++) {
                int source = SourceOffset(layout, x + pixel % 4, y + pixel / 4); int selected = 3;
                if (!transparent || rgba[source + 3] >= 128) {
                    int best = int.MaxValue;
                    for (int index = 0; index < (transparent ? 3 : 4); index++) {
                        int red = rgba[source] - Channel(first, second, index, 0); int green = rgba[source + 1] - Channel(first, second, index, 1); int blue = rgba[source + 2] - Channel(first, second, index, 2);
                        int candidate = red * red + green * green + blue * blue;
                        if (candidate < best) { best = candidate; selected = index; }
                    }
                }
                output[target + 4 + pixel / 4] |= (byte)(selected << (6 - pixel % 4 * 2));
            }
        }
        /// <summary>Returns a clamped source pixel for complete edge-tile encoding.</summary>
        static int SourceOffset(GxNativeTextureLayout layout, int x, int y) { return (Math.Min(y, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x, layout.RealWidth - 1)) * 4; }
        /// <summary>Expands an endpoint or the GX three-eighths or average interpolant for one color channel.</summary>
        static int Channel(int first, int second, int index, int channel) {
            int a = channel == 0 ? GxNativeTexturePixelCodec.Expand5(first >> 11) : channel == 1 ? GxNativeTexturePixelCodec.Expand6(first >> 5) : GxNativeTexturePixelCodec.Expand5(first);
            int b = channel == 0 ? GxNativeTexturePixelCodec.Expand5(second >> 11) : channel == 1 ? GxNativeTexturePixelCodec.Expand6(second >> 5) : GxNativeTexturePixelCodec.Expand5(second);
            if (index == 0) return a;
            if (index == 1) return b;
            if (first <= second) return (a + b) / 2;
            return index == 2 ? (a * 5 + b * 3) >> 3 : (a * 3 + b * 5) >> 3;
        }
    }
}
