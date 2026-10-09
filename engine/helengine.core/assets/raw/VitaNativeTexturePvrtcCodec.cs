namespace helengine {
    /// <summary>Reconstructs PVRTC I/II neighboring words, including checkerboard modulation and second-generation local palettes.</summary>
    public static class VitaNativeTexturePvrtcCodec {
        /// <summary>Encodes edge-clamped word means into valid PVRTC endpoint words in native Morton order.</summary>
        public static void Encode(byte[] rgba, VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start) {
            int width = layout.Format.BlockWidth;
            for (int y = 0; y < layout.StorageHeight; y += 4) for (int x = 0; x < layout.StorageWidth; x += width) {
                int red = 0; int green = 0; int blue = 0; int coverage = 0;
                for (int dy = 0; dy < 4; dy++) for (int dx = 0; dx < width; dx++) {
                    int source = (Math.Min(y + dy, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x + dx, layout.RealWidth - 1)) * 4;
                    red += VitaNativeTexturePixelCodec.SourceComponent(rgba, source, layout.Format, 0, alpha); green += VitaNativeTexturePixelCodec.SourceComponent(rgba, source, layout.Format, 1, alpha); blue += VitaNativeTexturePixelCodec.SourceComponent(rgba, source, layout.Format, 2, alpha); coverage += VitaNativeTexturePixelCodec.SourceComponent(rgba, source, layout.Format, 3, alpha);
                }
                int count = width * 4; red = (red + count / 2) / count; green = (green + count / 2) / count; blue = (blue + count / 2) / count; coverage = (coverage + count / 2) / count;
                uint color;
                if (coverage == 255) color = 0x80008000u | (uint)((red * 31 + 127) / 255) << 10 | (uint)((green * 31 + 127) / 255) << 5 | (uint)((blue * 15 + 127) / 255) << 1 | (uint)((red * 31 + 127) / 255) << 26 | (uint)((green * 31 + 127) / 255) << 21 | (uint)((blue * 31 + 127) / 255) << 16;
                else color = (uint)((coverage * 7 + 127) / 255) << 12 | (uint)((red * 15 + 127) / 255) << 8 | (uint)((green * 15 + 127) / 255) << 4 | (uint)((blue * 7 + 127) / 255) << 1 | (uint)((coverage * 7 + 127) / 255) << 28 | (uint)((red * 15 + 127) / 255) << 24 | (uint)((green * 15 + 127) / 255) << 20 | (uint)((blue * 15 + 127) / 255) << 16;
                int target = start + layout.GetBlockOffset(x / width, y / 4); VitaNativeTextureCodec.WriteWord(output, target, 0); VitaNativeTextureCodec.WriteWord(output, target + 4, color);
            }
        }
        /// <summary>Decodes complete native word neighborhoods and crops the final sampled image to its real dimensions.</summary>
        public static void Decode(byte[] input, int start, VitaNativeTextureLayout layout, byte[] rgba) {
            int[] values = new int[128];
            try { DecodeWithValues(input, start, layout, rgba, values); }
            finally { NativeOwnership.Release(ref values); }
        }
        /// <summary>Owns modulation modes separately so allocation and cleanup lifetimes remain unambiguous for native generation.</summary>
        static void DecodeWithValues(byte[] input, int start, VitaNativeTextureLayout layout, byte[] rgba, int[] values) {
            int[] modes = new int[128];
            try {
                int width = layout.Format.BlockWidth; int columns = layout.StorageWidth / width; int rows = layout.StorageHeight / 4; bool second = layout.Format.BaseFormat >= 0x82;
                for (int wordY = -1; wordY < rows - 1; wordY++) for (int wordX = -1; wordX < columns - 1; wordX++) {
                    for (int index = 0; index < 128; index++) { values[index] = 0; modes[index] = 0; }
                    int p = start + layout.GetBlockOffset((wordX + columns) % columns, (wordY + rows) % rows); int q = start + layout.GetBlockOffset((wordX + 1 + columns) % columns, (wordY + rows) % rows);
                    int r = start + layout.GetBlockOffset((wordX + columns) % columns, (wordY + 1 + rows) % rows); int s = start + layout.GetBlockOffset((wordX + 1 + columns) % columns, (wordY + 1 + rows) % rows);
                    uint pc = VitaNativeTextureCodec.ReadWord(input, p + 4); uint qc = VitaNativeTextureCodec.ReadWord(input, q + 4); uint rc = VitaNativeTextureCodec.ReadWord(input, r + 4); uint sc = VitaNativeTextureCodec.ReadWord(input, s + 4);
                    Unpack(VitaNativeTextureCodec.ReadWord(input, p), pc, pc, 0, 0, width, second, values, modes);
                    Unpack(VitaNativeTextureCodec.ReadWord(input, q), qc, pc, width, 0, width, second, values, modes);
                    Unpack(VitaNativeTextureCodec.ReadWord(input, r), rc, pc, 0, 4, width, second, values, modes);
                    Unpack(VitaNativeTextureCodec.ReadWord(input, s), sc, pc, width, 4, width, second, values, modes);
                    for (int y = 0; y < 4; y++) for (int x = 0; x < width; x++) {
                        int modulation = Modulation(values, modes, x + width / 2, y + 2, width); int tx = width == 4 ? y : x; int ty = width == 4 ? x : y;
                        int destinationX = ((wordX * width + width / 2 + tx) + layout.StorageWidth) % layout.StorageWidth; int destinationY = ((wordY * 4 + 2 + ty) + layout.StorageHeight) % layout.StorageHeight;
                        if (destinationX >= layout.RealWidth || destinationY >= layout.RealHeight) continue;
                        int target = (destinationY * layout.RealWidth + destinationX) * 4;
                        for (int channel = 0; channel < 4; channel++) {
                            int component = layout.Format.GetSwizzleComponent(channel);
                            rgba[target + channel] = component == 4 ? (byte)0 : component == 5 ? (byte)255 : (byte)Reconstruct(pc, qc, rc, sc, width, second, x, y, modulation, component);
                        }
                    }
                }
            } finally { NativeOwnership.Release(ref modes); }
        }
        /// <summary>Unpacks binary, checkerboard and four-level modes; second-generation hard transitions carry explicit mode flags.</summary>
        static void Unpack(uint bits, uint color, uint northWest, int ox, int oy, int width, bool second, int[] values, int[] modes) {
            int mode = (int)(color & 1); bool hard = second && (northWest & 0x8000) != 0;
            if (width == 8) {
                if (mode != 0) {
                    if ((bits & 1) != 0) { mode = (bits & (1u << 20)) != 0 ? 3 : 2; bits = (bits & ~(1u << 20)) | ((bits & (1u << 21)) >> 1); }
                    bits = (bits & ~1u) | ((bits & 2) >> 1);
                }
                for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) {
                    int index = (x + ox) * 8 + y + oy; modes[index] = mode + (hard && x + ox >= 6 && x + ox <= 9 && y + oy >= 2 && y + oy <= 5 ? 20 : 0);
                    if (mode == 0) { values[index] = (bits & 1) != 0 ? 3 : 0; bits >>= 1; }
                    else if (((x ^ y) & 1) == 0) { values[index] = (int)(bits & 3); bits >>= 2; }
                }
            } else {
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) {
                    int value = (int)(bits & 3); bits >>= 2; bool local = hard && x + ox >= 2 && x + ox <= 5 && y + oy >= 2 && y + oy <= 5;
                    if (mode != 0) value = local ? value + 30 : value == 0 ? 0 : value == 1 ? 4 : value == 2 ? 14 : 8;
                    else value = (value == 0 ? 0 : value == 1 ? 3 : value == 2 ? 5 : 8) + (local ? 20 : 0);
                    values[(y + oy) * 8 + x + ox] = value;
                }
            }
        }
        /// <summary>Reconstructs missing checkerboard weights using horizontal, vertical or four-neighbor interpolation.</summary>
        static int Modulation(int[] values, int[] modes, int x, int y, int width) {
            int index = x * 8 + y;
            if (width == 4) return values[index];
            int mode = modes[index] % 10; int flag = modes[index] - mode;
            if (mode == 0 || ((x ^ y) & 1) == 0) return Weight(values[index]) + flag;
            if (mode == 1) return (Weight(values[index - 8]) + Weight(values[index + 8]) + Weight(values[index - 1]) + Weight(values[index + 1]) + 2) / 4 + flag;
            if (mode == 2) return (Weight(values[index - 8]) + Weight(values[index + 8]) + 1) / 2 + flag;
            return (Weight(values[index - 1]) + Weight(values[index + 1]) + 1) / 2 + flag;
        }
        /// <summary>Maps a stored two-bit interpolation selector to its eight-step weight.</summary>
        static int Weight(int value) { return value == 0 ? 0 : value == 1 ? 3 : value == 2 ? 5 : 8; }
        /// <summary>Combines interpolated endpoints, punch-through coverage or the second-generation local palette.</summary>
        static int Reconstruct(uint p, uint q, uint r, uint s, int width, bool second, int x, int y, int modulation, int component) {
            if (modulation >= 30) return Palette(p, q, r, s, y * 4 + x, modulation - 30, component);
            int a; int b;
            if (modulation >= 20) { uint word = x < width / 2 ? y < 2 ? p : q : y < 2 ? r : s; a = Expand(Endpoint(word, false, true, component), width, component); b = Expand(Endpoint(word, true, true, component), width, component); modulation -= 20; }
            else { a = Interpolate(p, q, r, s, width, second, x, y, false, component); b = Interpolate(p, q, r, s, width, second, x, y, true, component); }
            bool transparent = modulation > 10; if (transparent) modulation -= 10;
            return transparent && component == 3 ? 0 : (a * (8 - modulation) + b * modulation) / 8;
        }
        /// <summary>Interpolates endpoint grids in their native sub-word coordinate convention before exact bit expansion.</summary>
        static int Interpolate(uint p, uint q, uint r, uint s, int width, bool second, int x, int y, bool b, int component) {
            int horizontal = width == 4 ? y : x; int vertical = width == 4 ? x : y;
            int value = (Endpoint(p, b, second, component) * (width - horizontal) + Endpoint(q, b, second, component) * horizontal) * (4 - vertical) + (Endpoint(r, b, second, component) * (width - horizontal) + Endpoint(s, b, second, component) * horizontal) * vertical;
            return Scale(value, width, component);
        }
        /// <summary>Expands a local endpoint without neighboring interpolation.</summary>
        static int Expand(int value, int width, int component) { return Scale(value * width * 4, width, component); }
        /// <summary>Expands five-bit RGB and four-bit alpha accumulated at the native bilinear scale.</summary>
        static int Scale(int value, int width, int component) {
            if (width == 8) return component == 3 ? (value >> 5) + (value >> 1) : (value >> 7) + (value >> 2);
            return component == 3 ? (value >> 4) + value : (value >> 6) + (value >> 1);
        }
        /// <summary>Unpacks endpoint components with PVRTC I's independent opacity and PVRTC II's shared opacity flag.</summary>
        static int Endpoint(uint word, bool b, bool second, int component) {
            bool opaque = (word & (b || second ? 0x80000000u : 0x8000u)) != 0;
            if (component == 3) return opaque ? 15 : b ? (int)((word >> 27) & 14) | (second ? 1 : 0) : (int)((word >> 11) & 14);
            if (b) {
                if (opaque) return (int)((word >> (component == 0 ? 26 : component == 1 ? 21 : 16)) & 31);
                int value = (int)((word >> (24 - component * 4)) & 15); return value << 1 | value >> 3;
            }
            if (opaque) {
                if (component < 2) return (int)((word >> (component == 0 ? 10 : 5)) & 31);
                int value = (int)((word >> 1) & 15); return value << 1 | value >> 3;
            }
            if (component < 2) { int value = (int)((word >> (8 - component * 4)) & 15); return value << 1 | value >> 3; }
            int blue = (int)((word >> 1) & 7); return blue << 2 | blue >> 1;
        }
        /// <summary>Resolves the sixteen local palettes used by four-bit PVRTC II hard-transition neighborhoods.</summary>
        static int Palette(uint p, uint q, uint r, uint s, int position, int selector, int component) {
            int ap = Expand(Endpoint(p, false, true, component), 4, component); int bp = Expand(Endpoint(p, true, true, component), 4, component);
            if (position == 0) return selector == 0 ? ap : selector == 1 ? (5 * ap + 3 * bp) / 8 : selector == 2 ? (3 * ap + 5 * bp) / 8 : bp;
            if (selector == 0) return position == 7 || position == 11 || position >= 14 ? Expand(Endpoint(s, false, true, component), 4, component) : ap;
            if (selector == 1) return position == 10 || position == 11 || position >= 13 ? Expand(Endpoint(s, true, true, component), 4, component) : bp;
            if (selector == 2) return position == 4 || position == 8 || position == 9 || position == 10 || position >= 12 ? Expand(Endpoint(r, false, true, component), 4, component) : Expand(Endpoint(q, false, true, component), 4, component);
            return position == 4 || position == 5 || position == 8 || position == 9 || position == 12 || position == 13 || position == 14 ? Expand(Endpoint(r, true, true, component), 4, component) : Expand(Endpoint(q, true, true, component), 4, component);
        }
    }
}
