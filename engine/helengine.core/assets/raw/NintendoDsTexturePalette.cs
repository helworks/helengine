namespace helengine {
    /// <summary>Builds deterministic RGB555 palettes using weighted farthest-color selection and native quantization.</summary>
    public static class NintendoDsTexturePalette {
        /// <summary>Quantizes an RGBA pixel into DS red-low RGB555 storage.</summary>
        public static int Color(byte[] rgba, int offset) => (rgba[offset] >> 3) | (rgba[offset + 1] >> 3) << 5 | (rgba[offset + 2] >> 3) << 10;
        /// <summary>Expands a native five-bit channel by replicating its high bits.</summary>
        public static byte Expand(int value) => (byte)((value << 3) | (value >> 2));
        /// <summary>Gets squared RGB555 distance without considering stored alpha.</summary>
        public static int Distance(int first, int second) {
            int red = (first & 31) - (second & 31), green = (first >> 5 & 31) - (second >> 5 & 31), blue = (first >> 10 & 31) - (second >> 10 & 31);
            return red * red + green * green + blue * blue;
        }
        /// <summary>Builds a fixed-capacity palette, reserving entry zero when binary indexed transparency is enabled.</summary>
        [NativeOwnedReturn]
        public static byte[] Build([NativeNoEscape] byte[] rgba, int capacity, bool transparent) {
            int[] histogram = new int[32768], distances = new int[32768];
            for (int offset = 0; offset < rgba.Length; offset += 4) if (!transparent || rgba[offset + 3] >= 128) histogram[Color(rgba, offset)]++;
            for (int color = 0; color < distances.Length; color++) distances[color] = 3072;
            byte[] result = new byte[capacity * 2];
            for (int entry = transparent ? 1 : 0; entry < capacity; entry++) {
                long bestScore = 0; int best = 0;
                for (int color = 0; color < histogram.Length; color++) {
                    long score = (long)histogram[color] * distances[color];
                    if (score > bestScore) { bestScore = score; best = color; }
                }
                if (bestScore == 0) break;
                result[entry * 2] = (byte)best; result[entry * 2 + 1] = (byte)(best >> 8);
                for (int color = 0; color < histogram.Length; color++) if (histogram[color] != 0) distances[color] = Math.Min(distances[color], Distance(color, best));
            }
            return result;
        }
        /// <summary>Finds the nearest usable native palette color; ties prefer the first entry.</summary>
        public static int Nearest(int color, [NativeNoEscape] byte[] palette, int first, int count) {
            int best = first, distance = int.MaxValue;
            for (int entry = first; entry < count; entry++) {
                int candidate = palette[entry * 2] | palette[entry * 2 + 1] << 8;
                int error = Distance(color, candidate);
                if (error < distance) { distance = error; best = entry; }
            }
            return best;
        }
    }
}
