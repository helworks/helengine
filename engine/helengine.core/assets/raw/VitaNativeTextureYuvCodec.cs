namespace helengine {
    /// <summary>Routes native GXM planar and paired YUV data with explicitly selected color-space conversion.</summary>
    public static class VitaNativeTextureYuvCodec {
        /// <summary>Encodes source pixels into canonical YUV plane or pair storage.</summary>
        public static void Encode(byte[] rgba, VitaNativeTextureLayout layout, byte[] output, int offset) {
            if (layout.Format.BaseFormat == 0x92) {
                for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x += 2) {
                    int first = Source(layout, x, y); int second = Source(layout, x + 1, y);
                    int u = (Chroma(rgba, first, false) + Chroma(rgba, second, false) + 1) / 2; int v = (Chroma(rgba, first, true) + Chroma(rgba, second, true) + 1) / 2;
                    int target = offset + y * layout.PitchBytes + x * 2; int order = layout.Format.Swizzle & 3;
                    output[target + (order < 2 ? 0 : 1)] = (byte)Luma(rgba, first); output[target + (order < 2 ? 2 : 3)] = (byte)Luma(rgba, second);
                    output[target + (order == 0 ? 1 : order == 1 ? 3 : order == 2 ? 0 : 2)] = (byte)u; output[target + (order == 0 ? 3 : order == 1 ? 1 : order == 2 ? 2 : 0)] = (byte)v;
                }
                return;
            }
            int plane = layout.StorageWidth * layout.StorageHeight;
            for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) output[offset + y * layout.StorageWidth + x] = (byte)Luma(rgba, Source(layout, x, y));
            for (int y = 0; y < layout.StorageHeight; y += 2) for (int x = 0; x < layout.StorageWidth; x += 2) {
                int first = Source(layout, x, y); int second = Source(layout, x + 1, y); int third = Source(layout, x, y + 1); int fourth = Source(layout, x + 1, y + 1);
                int u = (Chroma(rgba, first, false) + Chroma(rgba, second, false) + Chroma(rgba, third, false) + Chroma(rgba, fourth, false) + 2) / 4;
                int v = (Chroma(rgba, first, true) + Chroma(rgba, second, true) + Chroma(rgba, third, true) + Chroma(rgba, fourth, true) + 2) / 4;
                int index = y / 2 * (layout.StorageWidth / 2) + x / 2; bool swap = (layout.Format.Swizzle & 1) != 0;
                if (layout.Format.BaseFormat == 0x90) { output[offset + plane + index * 2] = (byte)(swap ? v : u); output[offset + plane + index * 2 + 1] = (byte)(swap ? u : v); }
                else { output[offset + plane + index] = (byte)(swap ? v : u); output[offset + plane + plane / 4 + index] = (byte)(swap ? u : v); }
            }
        }
        /// <summary>Decodes native YUV plane or pair storage through its declared conversion matrix.</summary>
        public static void Decode(byte[] input, int offset, VitaNativeTextureLayout layout, byte[] rgba) {
            int plane = layout.StorageWidth * layout.StorageHeight;
            for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) {
                int luma; int u; int v;
                if (layout.Format.BaseFormat == 0x92) {
                    int source = offset + y * layout.PitchBytes + x / 2 * 4; int order = layout.Format.Swizzle & 3;
                    luma = input[source + (order < 2 ? x % 2 * 2 : x % 2 * 2 + 1)];
                    u = input[source + (order == 0 ? 1 : order == 1 ? 3 : order == 2 ? 0 : 2)]; v = input[source + (order == 0 ? 3 : order == 1 ? 1 : order == 2 ? 2 : 0)];
                } else {
                    luma = input[offset + y * layout.StorageWidth + x]; int index = y / 2 * (layout.StorageWidth / 2) + x / 2; bool swap = (layout.Format.Swizzle & 1) != 0;
                    int first = layout.Format.BaseFormat == 0x90 ? input[offset + plane + index * 2] : input[offset + plane + index];
                    int second = layout.Format.BaseFormat == 0x90 ? input[offset + plane + index * 2 + 1] : input[offset + plane + plane / 4 + index];
                    u = swap ? second : first; v = swap ? first : second;
                }
                int target = (y * layout.RealWidth + x) * 4; int c = luma - 16; u -= 128; v -= 128;
                rgba[target] = (byte)Clamp((298 * c + 409 * v + 128) >> 8); rgba[target + 1] = (byte)Clamp((298 * c - 100 * u - 208 * v + 128) >> 8); rgba[target + 2] = (byte)Clamp((298 * c + 516 * u + 128) >> 8); rgba[target + 3] = 255;
            }
        }
        /// <summary>Clamps logical source coordinates for complete scanline and chroma-plane padding.</summary>
        static int Source(VitaNativeTextureLayout layout, int x, int y) { return (Math.Min(y, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x, layout.RealWidth - 1)) * 4; }
        /// <summary>Computes BT.601 standard-range luma for the canonical context profile.</summary>
        static int Luma(byte[] rgba, int source) { return Clamp(((66 * rgba[source] + 129 * rgba[source + 1] + 25 * rgba[source + 2] + 128) >> 8) + 16); }
        /// <summary>Computes BT.601 standard-range chroma before subsampled averaging.</summary>
        static int Chroma(byte[] rgba, int source, bool red) { return Clamp(((red ? 112 * rgba[source] - 94 * rgba[source + 1] - 18 * rgba[source + 2] : -38 * rgba[source] - 74 * rgba[source + 1] + 112 * rgba[source + 2]) + 128 >> 8) + 128); }
        /// <summary>Clamps converted channels to native byte range.</summary>
        static int Clamp(int value) { return Math.Min(255, Math.Max(0, value)); }
    }
}
