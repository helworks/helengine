namespace helengine {
    /// <summary>Encodes ETC1 individual blocks and decodes both ETC1 modes in the 3DS little-endian tile layout.</summary>
    public static class Nintendo3DsEtcTextureCodec {
        /// <summary>Stores the eight standard ETC1 modifier tables in selector order.</summary>
        static readonly int[] Modifiers = new int[] { 2, 8, -2, -8, 5, 17, -5, -17, 9, 29, -9, -29, 13, 42, -13, -42, 18, 60, -18, -60, 24, 80, -24, -80, 33, 106, -33, -106, 47, 183, -47, -183 };
        /// <summary>Encodes all padded 4-by-4 blocks, with column-major alpha nibbles before each ETC1A4 color block.</summary>
        public static void Encode(byte[] rgba, int width, int height, Nintendo3DsTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start) {
            for (int y = 0; y < layout.StorageHeight; y += 4) {
                for (int x = 0; x < layout.StorageWidth; x += 4) {
                    int offset = start + Nintendo3DsTextureLayout.BlockIndex(x, y, layout.StorageWidth) * (layout.Format.Code == 12 ? 8 : 16);
                    if (layout.Format.Code == 13) {
                        ulong coverage = 0;
                        for (int dx = 0; dx < 4; dx++) {
                            for (int dy = 0; dy < 4; dy++) {
                                int value = alpha == TextureAssetAlphaPrecision.Opaque ? 255 : rgba[SourceOffset(x + dx, y + dy, width, height) + 3];
                                coverage |= (ulong)((value * 15 + 127) / 255) << ((dx * 4 + dy) * 4);
                            }
                        }
                        WriteWord(output, offset, coverage); offset += 8;
                    }
                    ulong best = 0;
                    long bestError = long.MaxValue;
                    for (int flip = 0; flip <= 1; flip++) {
                        ulong first = EncodeHalf(rgba, width, height, x, y, flip, 0, out long firstError);
                        ulong second = EncodeHalf(rgba, width, height, x, y, flip, 1, out long secondError);
                        if (firstError + secondError < bestError) { bestError = firstError + secondError; best = first | second | (ulong)flip << 32; }
                    }
                    WriteWord(output, offset, best);
                }
            }
        }
        /// <summary>Fits each half's quantized average and neighboring brightness to the best table and pixel selectors.</summary>
        static ulong EncodeHalf(byte[] rgba, int width, int height, int blockX, int blockY, int flip, int half, out long error) {
            int red = 0, green = 0, blue = 0;
            for (int y = 0; y < 4; y++) {
                for (int x = 0; x < 4; x++) {
                    if ((flip == 0 ? x / 2 : y / 2) != half) continue;
                    int source = SourceOffset(blockX + x, blockY + y, width, height);
                    red += rgba[source]; green += rgba[source + 1]; blue += rgba[source + 2];
                }
            }
            red = (red * 15 + 1020) / 2040; green = (green * 15 + 1020) / 2040; blue = (blue * 15 + 1020) / 2040;
            error = long.MaxValue;
            ulong result = 0;
            for (int adjustment = -1; adjustment <= 1; adjustment++) {
                int r = Math.Clamp(red + adjustment, 0, 15), g = Math.Clamp(green + adjustment, 0, 15), b = Math.Clamp(blue + adjustment, 0, 15);
                for (int table = 0; table < 8; table++) {
                    long candidateError = 0;
                    ulong selectors = 0;
                    for (int y = 0; y < 4; y++) {
                        for (int x = 0; x < 4; x++) {
                            if ((flip == 0 ? x / 2 : y / 2) != half) continue;
                            int source = SourceOffset(blockX + x, blockY + y, width, height);
                            int bestSelector = 0, pixelError = int.MaxValue;
                            for (int selector = 0; selector < 4; selector++) {
                                int modifier = Modifiers[table * 4 + selector];
                                int dr = rgba[source] - Math.Clamp(r * 17 + modifier, 0, 255);
                                int dg = rgba[source + 1] - Math.Clamp(g * 17 + modifier, 0, 255);
                                int db = rgba[source + 2] - Math.Clamp(b * 17 + modifier, 0, 255);
                                int distance = dr * dr + dg * dg + db * db;
                                if (distance < pixelError) { pixelError = distance; bestSelector = selector; }
                            }
                            candidateError += pixelError;
                            int index = x * 4 + y;
                            selectors |= (ulong)(bestSelector & 1) << index | (ulong)(bestSelector >> 1) << (index + 16);
                        }
                    }
                    if (candidateError < error) {
                        error = candidateError;
                        result = (ulong)r << (60 - half * 4) | (ulong)g << (52 - half * 4) | (ulong)b << (44 - half * 4)
                            | (ulong)table << (37 - half * 3) | selectors;
                    }
                }
            }
            return result;
        }
        /// <summary>Samples ETC1 or ETC1A4 and rejects differential endpoints outside the ETC1 component range.</summary>
        public static void Sample(byte[] data, int offset, int code, int x, int y, byte[] rgba, int target) {
            ulong coverage = code == 13 ? ReadWord(data, offset) : ulong.MaxValue;
            ulong word = ReadWord(data, offset + (code == 13 ? 8 : 0));
            int half = (word >> 32 & 1) == 0 ? x / 2 : y / 2;
            bool differential = (word >> 33 & 1) != 0;
            int red = Component(word, 60, half, differential), green = Component(word, 52, half, differential), blue = Component(word, 44, half, differential);
            int table = (int)(word >> (37 - half * 3) & 7);
            int index = x * 4 + y;
            int selector = (int)(word >> index & 1) | (int)(word >> (index + 15) & 2);
            int modifier = Modifiers[table * 4 + selector];
            rgba[target] = (byte)Math.Clamp(red + modifier, 0, 255);
            rgba[target + 1] = (byte)Math.Clamp(green + modifier, 0, 255);
            rgba[target + 2] = (byte)Math.Clamp(blue + modifier, 0, 255);
            rgba[target + 3] = code == 13 ? (byte)((coverage >> (index * 4) & 15) * 17) : (byte)255;
        }
        /// <summary>Resolves an individual or signed-differential color component and expands its high bits.</summary>
        static int Component(ulong word, int highShift, int half, bool differential) {
            if (!differential) return (int)(word >> (highShift - half * 4) & 15) * 17;
            int component = (int)(word >> (highShift - 1) & 31);
            if (half != 0) {
                int delta = (int)(word >> (highShift - 4) & 7);
                component += delta >= 4 ? delta - 8 : delta;
            }
            if (component < 0 || component > 31) throw new ArgumentException("Invalid ETC1 differential endpoint.");
            return component << 3 | component >> 2;
        }
        /// <summary>Reads a bounded little-endian 64-bit PICA block.</summary>
        public static ulong ReadWord(byte[] data, int offset) {
            if (data == null || offset < 0 || data.Length - offset < 8) throw new ArgumentException("Truncated 3DS ETC block.");
            ulong result = 0;
            for (int index = 0; index < 8; index++) result |= (ulong)data[offset + index] << (index * 8);
            return result;
        }
        /// <summary>Writes one ETC or alpha block in 3DS little-endian byte order.</summary>
        static void WriteWord(byte[] output, int offset, ulong word) {
            for (int index = 0; index < 8; index++) output[offset + index] = (byte)(word >> (index * 8));
        }
        /// <summary>Clamps padded coordinates to the last authored source texel.</summary>
        static int SourceOffset(int x, int y, int width, int height) => (Math.Min(y, height - 1) * width + Math.Min(x, width - 1)) * 4;
    }
}
