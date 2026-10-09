namespace helengine {
    /// <summary>Encodes and samples native UBC/SBC blocks with their little-endian BC interpolation and signed endpoints.</summary>
    public static class VitaNativeTextureBcCodec {
        /// <summary>Encodes edge-clamped physical channels into 4-by-4 blocks without changing the selected hardware format.</summary>
        public static void Encode(byte[] rgba, VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start) {
            for (int y = 0; y < layout.StorageHeight; y += 4) for (int x = 0; x < layout.StorageWidth; x += 4) {
                int target = start + layout.GetBlockOffset(x / 4, y / 4); int code = layout.Format.BaseFormat;
                if (code >= 0x88) {
                    EncodeChannel(rgba, layout, x, y, alpha, 0, code == 0x89 || code == 0x8b, output, target);
                    if (code >= 0x8a) EncodeChannel(rgba, layout, x, y, alpha, 1, code == 0x8b, output, target + 8);
                } else {
                    if (code == 0x86) {
                        ulong coverage = 0;
                        for (int pixel = 0; pixel < 16; pixel++) coverage |= (ulong)((Physical(rgba, layout, x, y, pixel, 3, alpha) * 15 + 127) / 255) << (pixel * 4);
                        VitaNativeTextureNumberCodec.Write(output, target, 8, coverage); target += 8;
                    } else if (code == 0x87) { EncodeChannel(rgba, layout, x, y, alpha, 3, false, output, target); target += 8; }
                    EncodeColor(rgba, layout, x, y, alpha, code == 0x85, output, target);
                }
            }
        }
        /// <summary>Samples logical pixels through the exact SDK channel swizzle, including BC4/5 signed normalization.</summary>
        public static void Decode(byte[] input, int start, VitaNativeTextureLayout layout, byte[] rgba) {
            for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) {
                int source = start + layout.GetBlockOffset(x / 4, y / 4); int pixel = (y & 3) * 4 + (x & 3); int target = (y * layout.RealWidth + x) * 4;
                for (int channel = 0; channel < 4; channel++) {
                    int component = layout.Format.GetSwizzleComponent(channel);
                    rgba[target + channel] = component == 4 ? (byte)0 : component == 5 ? (byte)255 : (byte)Sample(input, source, layout.Format.BaseFormat, pixel, component);
                }
            }
        }
        /// <summary>Samples a physical component from standard BC color or alpha blocks.</summary>
        static int Sample(byte[] input, int source, int code, int pixel, int component) {
            if (code >= 0x88) {
                int value = Channel(input, source + component * 8, pixel, code == 0x89 || code == 0x8b);
                return code == 0x89 || code == 0x8b ? Math.Max(0, value) * 255 / 127 : value;
            }
            if (component == 3) {
                if (code == 0x86) return (int)((VitaNativeTextureNumberCodec.Read(input, source, 8) >> (pixel * 4)) & 15) * 17;
                if (code == 0x87) return Channel(input, source, pixel, false);
            }
            int color = source + (code == 0x85 ? 0 : 8);
            int first = input[color] | input[color + 1] << 8; int second = input[color + 2] | input[color + 3] << 8;
            int selector = (int)((VitaNativeTextureCodec.ReadWord(input, color + 4) >> (pixel * 2)) & 3);
            if (component == 3) return code == 0x85 && first <= second && selector == 3 ? 0 : 255;
            return Color(first, second, selector, component, code != 0x85 || first > second);
        }
        /// <summary>Expands RGB565 endpoint bits by replication and applies BC1 three- or four-color interpolation.</summary>
        static int Color(int first, int second, int selector, int component, bool fourColor) {
            int a = Endpoint(first, component); int b = Endpoint(second, component);
            if (selector == 0) return a;
            if (selector == 1) return b;
            if (fourColor) return selector == 2 ? (2 * a + b) / 3 : (a + 2 * b) / 3;
            return selector == 2 ? (a + b) / 2 : 0;
        }
        /// <summary>Expands a physical red, green or blue component from a BC RGB565 endpoint.</summary>
        static int Endpoint(int word, int component) {
            int value = component == 0 ? word >> 11 : component == 1 ? (word >> 5) & 63 : word & 31;
            return component == 1 ? value << 2 | value >> 4 : value << 3 | value >> 2;
        }
        /// <summary>Decodes an unsigned or signed BC alpha selector using the required six- or eight-value table.</summary>
        static int Channel(byte[] input, int source, int pixel, bool signed) {
            int first = signed ? Math.Max(-127, (int)(sbyte)input[source]) : input[source];
            int second = signed ? Math.Max(-127, (int)(sbyte)input[source + 1]) : input[source + 1];
            int selector = (int)((VitaNativeTextureNumberCodec.Read(input, source + 2, 6) >> (pixel * 3)) & 7);
            return ChannelValue(first, second, selector, signed);
        }
        /// <summary>Constructs one BC alpha table entry; signed interpolation truncates toward zero as specified.</summary>
        static int ChannelValue(int first, int second, int selector, bool signed) {
            if (selector == 0) return first;
            if (selector == 1) return second;
            if (first > second) return ((8 - selector) * first + (selector - 1) * second) / 7;
            if (selector < 6) return ((6 - selector) * first + (selector - 1) * second) / 5;
            return selector == 6 ? signed ? -127 : 0 : signed ? 127 : 255;
        }
        /// <summary>Fits native BC4 or alpha endpoints and nearest selectors to the selected physical source channel.</summary>
        static void EncodeChannel(byte[] rgba, VitaNativeTextureLayout layout, int x, int y, TextureAssetAlphaPrecision alpha, int component, bool signed, byte[] output, int target) {
            int first = 0; int second = signed ? 127 : 255;
            for (int pixel = 0; pixel < 16; pixel++) { int value = Physical(rgba, layout, x, y, pixel, component, alpha); if (signed) value = (value * 127 + 127) / 255; first = Math.Max(first, value); second = Math.Min(second, value); }
            output[target] = (byte)first; output[target + 1] = (byte)second; ulong selectors = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int value = Physical(rgba, layout, x, y, pixel, component, alpha); if (signed) value = (value * 127 + 127) / 255;
                int best = 0; int error = int.MaxValue;
                for (int selector = 0; selector < 8; selector++) { int distance = Math.Abs(value - ChannelValue(first, second, selector, signed)); if (distance < error) { error = distance; best = selector; } }
                selectors |= (ulong)best << (pixel * 3);
            }
            VitaNativeTextureNumberCodec.Write(output, target + 2, 6, selectors);
        }
        /// <summary>Fits a farthest physical RGB pair and chooses selectors, reserving the transparent code for BC1 coverage.</summary>
        static void EncodeColor(byte[] rgba, VitaNativeTextureLayout layout, int x, int y, TextureAssetAlphaPrecision alpha, bool bc1, byte[] output, int target) {
            int first = 0; int second = 0; int maximum = -1; bool transparent = false;
            for (int a = 0; a < 16; a++) {
                if (bc1 && Physical(rgba, layout, x, y, a, 3, alpha) < 128) { transparent = true; continue; }
                for (int b = a; b < 16; b++) {
                    if (bc1 && Physical(rgba, layout, x, y, b, 3, alpha) < 128) continue;
                    int distance = 0;
                    for (int component = 0; component < 3; component++) { int delta = Physical(rgba, layout, x, y, a, component, alpha) - Physical(rgba, layout, x, y, b, component, alpha); distance += delta * delta; }
                    if (distance > maximum) { maximum = distance; first = PackColor(rgba, layout, x, y, a, alpha); second = PackColor(rgba, layout, x, y, b, alpha); }
                }
            }
            if (transparent ? first > second : first < second) { int temporary = first; first = second; second = temporary; }
            if (!transparent && first == second) { if (first < 65535) first++; else second--; }
            output[target] = (byte)first; output[target + 1] = (byte)(first >> 8); output[target + 2] = (byte)second; output[target + 3] = (byte)(second >> 8); uint selectors = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int best = 0; int error = int.MaxValue;
                if (transparent && Physical(rgba, layout, x, y, pixel, 3, alpha) < 128) best = 3;
                else for (int selector = 0; selector < (transparent ? 3 : 4); selector++) {
                    int distance = 0;
                    for (int component = 0; component < 3; component++) { int delta = Physical(rgba, layout, x, y, pixel, component, alpha) - Color(first, second, selector, component, !transparent); distance += delta * delta; }
                    if (distance < error) { error = distance; best = selector; }
                }
                selectors |= (uint)best << (pixel * 2);
            }
            VitaNativeTextureCodec.WriteWord(output, target + 4, selectors);
        }
        /// <summary>Quantizes one physical source RGB value to a BC endpoint.</summary>
        static int PackColor(byte[] rgba, VitaNativeTextureLayout layout, int x, int y, int pixel, TextureAssetAlphaPrecision alpha) {
            return (Physical(rgba, layout, x, y, pixel, 0, alpha) * 31 + 127) / 255 << 11 | (Physical(rgba, layout, x, y, pixel, 1, alpha) * 63 + 127) / 255 << 5 | (Physical(rgba, layout, x, y, pixel, 2, alpha) * 31 + 127) / 255;
        }
        /// <summary>Reads an edge-clamped channel after inversion of the native sampling swizzle.</summary>
        static int Physical(byte[] rgba, VitaNativeTextureLayout layout, int x, int y, int pixel, int component, TextureAssetAlphaPrecision alpha) {
            int source = (Math.Min(y + pixel / 4, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x + pixel % 4, layout.RealWidth - 1)) * 4;
            return VitaNativeTexturePixelCodec.SourceComponent(rgba, source, layout.Format, component, alpha);
        }
    }
}
