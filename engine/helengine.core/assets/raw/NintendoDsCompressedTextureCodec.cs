namespace helengine {
    /// <summary>Encodes native 4x4 selector blocks with reusable contiguous palettes and decodes all four interpolation modes.</summary>
    public static class NintendoDsCompressedTextureCodec {
        /// <summary>Encodes padded blocks in native mode zero or two and returns their separately owned RGB555 palette.</summary>
        [NativeOwnedReturn]
        public static byte[] Encode([NativeNoEscape] byte[] rgba, [NativeNoEscape] NintendoDsTextureLayout layout, TextureAssetAlphaPrecision alpha, [NativeNoEscape] byte[] output, int start) {
            List<byte> palette = new List<byte>();
            Dictionary<ulong, int> groups = new Dictionary<ulong, int>();
            int[] colors = new int[16], counts = new int[16], distances = new int[16];
            byte[] blockPalette = new byte[8];
            for (int y = 0; y < layout.StorageHeight; y += 4) {
                for (int x = 0; x < layout.StorageWidth; x += 4) {
                    int unique = 0; bool transparent = false;
                    for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) {
                        int source = Source(x + column, y + row, layout.Width, layout.Height);
                        if (alpha != TextureAssetAlphaPrecision.Opaque && rgba[source + 3] < 128) { transparent = true; continue; }
                        int color = NintendoDsTexturePalette.Color(rgba, source), found = -1;
                        for (int index = 0; index < unique; index++) if (colors[index] == color) found = index;
                        if (found < 0) { found = unique++; colors[found] = color; counts[found] = 0; distances[found] = 3072; }
                        counts[found]++;
                    }
                    int capacity = transparent ? 3 : 4;
                    for (int entry = 0; entry < 4; entry++) {
                        int best = 0, score = -1;
                        for (int index = 0; index < unique && entry < capacity; index++) if (counts[index] * distances[index] > score) { score = counts[index] * distances[index]; best = colors[index]; }
                        blockPalette[entry * 2] = (byte)best; blockPalette[entry * 2 + 1] = (byte)(best >> 8);
                        for (int index = 0; index < unique; index++) distances[index] = Math.Min(distances[index], NintendoDsTexturePalette.Distance(colors[index], best));
                    }
                    ulong key = 0;
                    for (int index = 0; index < 8; index++) key |= (ulong)blockPalette[index] << (index * 8);
                    if (!groups.TryGetValue(key, out int baseEntry)) {
                        if (palette.Count + 8 > 65536) throw new ArgumentException("DS compressed texture requires more than 32768 native palette colors.");
                        baseEntry = palette.Count / 2; groups.Add(key, baseEntry);
                        for (int index = 0; index < 8; index++) palette.Add(blockPalette[index]);
                    }
                    int block = NintendoDsTextureLayout.BlockIndex(x, y, layout.StorageWidth);
                    int descriptor = baseEntry / 2 | (transparent ? 0 : 2) << 14;
                    output[start + layout.TexelBytes + block * 2] = (byte)descriptor; output[start + layout.TexelBytes + block * 2 + 1] = (byte)(descriptor >> 8);
                    for (int row = 0; row < 4; row++) {
                        int selectors = 0;
                        for (int column = 0; column < 4; column++) {
                            int source = Source(x + column, y + row, layout.Width, layout.Height);
                            int selector = transparent && rgba[source + 3] < 128 ? 3 : NintendoDsTexturePalette.Nearest(NintendoDsTexturePalette.Color(rgba, source), blockPalette, 0, capacity);
                            selectors |= selector << (column * 2);
                        }
                        output[start + block * 4 + row] = (byte)selectors;
                    }
                }
            }
            return palette.ToArray();
        }
        /// <summary>Gets an edge-clamped RGBA address for padding partial blocks.</summary>
        static int Source(int x, int y, int width, int height) => (Math.Min(y, height - 1) * width + Math.Min(x, width - 1)) * 4;
        /// <summary>Samples direct, half-average, or 5:3/3:5 RGB555 interpolation and native transparent selector three.</summary>
        public static uint Sample([NativeNoEscape] byte[] data, int start, [NativeNoEscape] byte[] palette, [NativeNoEscape] NintendoDsTextureLayout layout, int x, int y) {
            int block = NintendoDsTextureLayout.BlockIndex(x, y, layout.StorageWidth);
            int descriptor = NintendoDsTextureCodec.Word(data, start + layout.TexelBytes + block * 2);
            int selector = data[start + block * 4 + y % 4] >> (x % 4 * 2) & 3;
            int mode = descriptor >> 14, entry = (descriptor & 16383) * 2;
            if (mode < 2 && selector == 3) return 0;
            int color;
            if (selector < 2 || mode == 0 || mode == 2) color = NintendoDsTextureCodec.Word(palette, (entry + selector) * 2);
            else {
                int first = NintendoDsTextureCodec.Word(palette, entry * 2), second = NintendoDsTextureCodec.Word(palette, (entry + 1) * 2);
                int weight = mode == 1 ? 4 : selector == 2 ? 5 : 3;
                color = Mix(first & 31, second & 31, weight) | Mix(first >> 5 & 31, second >> 5 & 31, weight) << 5 | Mix(first >> 10 & 31, second >> 10 & 31, weight) << 10;
            }
            return NintendoDsTextureCodec.Rgba(color, 31);
        }
        /// <summary>Interpolates a five-bit component with hardware truncation.</summary>
        static int Mix(int first, int second, int weight) => (first * weight + second * (8 - weight)) / 8;
    }
}
