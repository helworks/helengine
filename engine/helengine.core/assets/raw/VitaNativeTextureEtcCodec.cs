namespace helengine {
    /// <summary>Encodes ETC1 individual blocks and decodes individual and differential ETC1 in native byte order.</summary>
    public static class VitaNativeTextureEtcCodec {
        /// <summary>Contains standard ETC1 modifiers in the two selector-plane order.</summary>
        static readonly int[] Modifiers = new int[] { 2, 8, -2, -8, 5, 17, -5, -17, 9, 29, -9, -29, 13, 42, -13, -42, 18, 60, -18, -60, 24, 80, -24, -80, 33, 106, -33, -106, 47, 183, -47, -183 };
        /// <summary>Fits complete edge-clamped ETC1 blocks and writes the standard big-endian block bytes.</summary>
        public static void Encode(byte[] rgba, VitaNativeTextureLayout layout, byte[] output, int start) {
            for (int y = 0; y < layout.StorageHeight; y += 4) for (int x = 0; x < layout.StorageWidth; x += 4) {
                ulong best = 0; long error = long.MaxValue;
                for (int flip = 0; flip < 2; flip++) {
                    ulong first = EncodeHalf(rgba, layout, x, y, flip, 0, out long firstError);
                    ulong second = EncodeHalf(rgba, layout, x, y, flip, 1, out long secondError);
                    if (firstError + secondError < error) { error = firstError + secondError; best = first | second | (ulong)flip << 32; }
                }
                int target = start + layout.GetBlockOffset(x / 4, y / 4);
                for (int index = 0; index < 8; index++) output[target + index] = (byte)(best >> (56 - index * 8));
            }
        }
        /// <summary>Samples actual logical texels from the standard ETC1 endpoint and selector planes.</summary>
        public static void Decode(byte[] input, int start, VitaNativeTextureLayout layout, byte[] rgba) {
            for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) {
                int source = start + layout.GetBlockOffset(x / 4, y / 4); ulong word = 0;
                for (int index = 0; index < 8; index++) word = word << 8 | input[source + index];
                int half = ((word >> 32) & 1) == 0 ? (x & 3) / 2 : (y & 3) / 2;
                bool differential = ((word >> 33) & 1) != 0; int selectorIndex = (x & 3) * 4 + (y & 3);
                int selector = (int)((word >> selectorIndex) & 1) | (int)((word >> (selectorIndex + 15)) & 2);
                int modifier = Modifiers[(int)((word >> (37 - half * 3)) & 7) * 4 + selector]; int target = (y * layout.RealWidth + x) * 4;
                for (int channel = 0; channel < 3; channel++) rgba[target + channel] = (byte)Math.Clamp(Component(word, 60 - channel * 8, half, differential) + modifier, 0, 255);
                rgba[target + 3] = 255;
            }
        }
        /// <summary>Fits each half's quantized mean and searches all modifier tables and selectors.</summary>
        static ulong EncodeHalf(byte[] rgba, VitaNativeTextureLayout layout, int blockX, int blockY, int flip, int half, out long error) {
            int red = 0; int green = 0; int blue = 0;
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) {
                if ((flip == 0 ? x / 2 : y / 2) != half) continue;
                int source = Source(layout, blockX + x, blockY + y); red += rgba[source]; green += rgba[source + 1]; blue += rgba[source + 2];
            }
            red = (red * 15 + 1020) / 2040; green = (green * 15 + 1020) / 2040; blue = (blue * 15 + 1020) / 2040;
            error = long.MaxValue; ulong result = 0;
            for (int table = 0; table < 8; table++) {
                long distance = 0; ulong selectors = 0;
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) {
                    if ((flip == 0 ? x / 2 : y / 2) != half) continue;
                    int source = Source(layout, blockX + x, blockY + y); int best = 0; int bestError = int.MaxValue;
                    for (int selector = 0; selector < 4; selector++) {
                        int modifier = Modifiers[table * 4 + selector]; int dr = rgba[source] - Math.Clamp(red * 17 + modifier, 0, 255); int dg = rgba[source + 1] - Math.Clamp(green * 17 + modifier, 0, 255); int db = rgba[source + 2] - Math.Clamp(blue * 17 + modifier, 0, 255);
                        int current = dr * dr + dg * dg + db * db; if (current < bestError) { bestError = current; best = selector; }
                    }
                    distance += bestError; int bit = x * 4 + y; selectors |= (ulong)(best & 1) << bit | (ulong)(best >> 1) << (bit + 16);
                }
                if (distance < error) { error = distance; result = (ulong)red << (60 - half * 4) | (ulong)green << (52 - half * 4) | (ulong)blue << (44 - half * 4) | (ulong)table << (37 - half * 3) | selectors; }
            }
            return result;
        }
        /// <summary>Expands four-bit endpoints or signed-delta five-bit endpoints and rejects invalid differential codes.</summary>
        static int Component(ulong word, int shift, int half, bool differential) {
            if (!differential) return (int)((word >> (shift - half * 4)) & 15) * 17;
            int value = (int)((word >> (shift - 1)) & 31);
            if (half != 0) { int delta = (int)((word >> (shift - 4)) & 7); value += delta < 4 ? delta : delta - 8; }
            if (value < 0 || value > 31) throw new ArgumentException("Invalid ETC1 differential endpoint.");
            return value << 3 | value >> 2;
        }
        /// <summary>Clamps padded source coordinates to the last authored pixel.</summary>
        static int Source(VitaNativeTextureLayout layout, int x, int y) { return (Math.Min(y, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x, layout.RealWidth - 1)) * 4; }
    }
}
