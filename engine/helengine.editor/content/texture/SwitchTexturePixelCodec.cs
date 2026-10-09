namespace helengine.editor {
    /// <summary>Converts scalar, packed, floating-point and depth/stencil Switch elements while preserving native little-endian words.</summary>
    public static class SwitchTexturePixelCodec {
        /// <summary>Encodes normalized RGBA source colors into tightly packed native component elements.</summary>
        public static byte[] Encode(byte[] rgba, int width, int height, SwitchTextureFormat format) {
            byte[] result = new byte[width * height * format.BytesPerBlock];
            int[] widths = Widths(format.Name); string channels = Channels(format.Name);
            for (int pixel = 0; pixel < width * height; pixel++) EncodePixel(rgba, pixel * 4, result, pixel * format.BytesPerBlock, format.Name, widths, channels);
            return result;
        }
        /// <summary>Decodes native typed values into a normalized RGBA8 CPU view; HDR and negative values are clamped only in this view.</summary>
        public static byte[] Decode(byte[] elements, int width, int height, SwitchTextureFormat format) {
            byte[] rgba = new byte[SwitchTextureCodec.PreviewBytes(width, height)];
            int[] widths = Widths(format.Name); string channels = Channels(format.Name);
            for (int pixel = 0; pixel < width * height; pixel++) DecodePixel(elements, pixel * format.BytesPerBlock, rgba, pixel * 4, format.Name, widths, channels);
            return rgba;
        }
        /// <summary>Extracts the physical channel sequence, including leading alpha and unused X channels.</summary>
        static string Channels(string name) {
            if (name.StartsWith("A1BGR5", StringComparison.Ordinal)) return "ABGR";
            if (name.StartsWith("BG", StringComparison.Ordinal)) return name.StartsWith("BGRA", StringComparison.Ordinal) || name.Contains("5A1", StringComparison.Ordinal) ? "BGRA" : name.StartsWith("BGRX", StringComparison.Ordinal) ? "BGRX" : "BGR";
            if (name.StartsWith("RGBA", StringComparison.Ordinal) || name.StartsWith("RGB10A2", StringComparison.Ordinal) || name.StartsWith("RGB5A1", StringComparison.Ordinal)) return "RGBA";
            if (name.StartsWith("RGBX", StringComparison.Ordinal)) return "RGBX";
            if (name.StartsWith("RGB", StringComparison.Ordinal)) return "RGB";
            return name.StartsWith("RG", StringComparison.Ordinal) ? "RG" : "R";
        }
        /// <summary>Gets widths for exact packed words or repeated-width component arrays.</summary>
        static int[] Widths(string name) {
            if (name.StartsWith("A1BGR5", StringComparison.Ordinal)) return new[] { 1, 5, 5, 5 };
            if (name.Contains("565", StringComparison.Ordinal)) return new[] { 5, 6, 5 };
            if (name.Contains("5A1", StringComparison.Ordinal)) return new[] { 5, 5, 5, 1 };
            if (name.StartsWith("RGB5_", StringComparison.Ordinal) || name.StartsWith("BGR5_", StringComparison.Ordinal)) return new[] { 5, 5, 5 };
            if (name.StartsWith("RGBA4_", StringComparison.Ordinal)) return new[] { 4, 4, 4, 4 };
            if (name.StartsWith("RGB10A2", StringComparison.Ordinal)) return new[] { 10, 10, 10, 2 };
            int bits = name.Contains("32", StringComparison.Ordinal) ? 32 : name.Contains("16", StringComparison.Ordinal) ? 16 : 8;
            int[] result = new int[Channels(name).Length]; Array.Fill(result, bits); return result;
        }
        /// <summary>Writes one native typed pixel, keeping stencil and padding independent of color alpha.</summary>
        static void EncodePixel(byte[] rgba, int source, byte[] output, int target, string name, int[] widths, string channels) {
            if (name.StartsWith("Z", StringComparison.Ordinal) || name == "S8") {
                if (name == "S8") output[target] = rgba[source];
                else if (name == "Z16") Write(output, target, 2, (ulong)(rgba[source] * 257));
                else if (name.StartsWith("ZF32", StringComparison.Ordinal)) {
                    Write(output, target, 4, (uint)BitConverter.SingleToInt32Bits(rgba[source] / 255f));
                    if (name == "ZF32_X24S8") output[target + 4] = rgba[source + 1];
                } else Write(output, target, 4, (ulong)Math.Round(rgba[source] / 255d * 16777215) << 8 | (name == "Z24S8" ? rgba[source + 1] : 0u));
                return;
            }
            if (name == "RG11B10_Float") {
                uint word = SmallFloat(rgba[source], 6) | SmallFloat(rgba[source + 1], 6) << 11 | SmallFloat(rgba[source + 2], 5) << 22;
                Write(output, target, 4, word); return;
            }
            if (name == "E5BGR9_Float") {
                double maximum = Math.Max(rgba[source], Math.Max(rgba[source + 1], rgba[source + 2])) / 255d;
                int exponent = maximum == 0 ? 0 : Math.Clamp((int)Math.Floor(Math.Log2(maximum)) + 16, 0, 31);
                double scale = Math.Pow(2, exponent - 24); uint word = (uint)exponent << 27;
                for (int channel = 0; channel < 3; channel++) word |= (uint)Math.Clamp((int)Math.Round(rgba[source + channel] / 255d / scale), 0, 511) << (channel * 9);
                Write(output, target, 4, word); return;
            }
            int shift = 0;
            for (int channel = 0; channel < widths.Length; channel++) {
                int bits = widths[channel], component = Component(channels[channel]);
                double value = component < 0 ? 1 : rgba[source + component] / 255d;
                ulong encoded;
                if (name.Contains("Float", StringComparison.Ordinal)) encoded = bits == 16 ? BitConverter.HalfToUInt16Bits((Half)value) : (uint)BitConverter.SingleToInt32Bits((float)value);
                else {
                    bool signed = name.Contains("Snorm", StringComparison.Ordinal) || name.Contains("Sint", StringComparison.Ordinal);
                    ulong maximum = (1UL << (signed ? bits - 1 : bits)) - 1;
                    encoded = (ulong)Math.Round(value * maximum);
                }
                if ((bits & 7) == 0 && (shift & 7) == 0) Write(output, target + shift / 8, bits / 8, encoded);
                else for (int bit = 0; bit < bits; bit++) if ((encoded & (1UL << bit)) != 0) output[target + (shift + bit) / 8] |= (byte)(1 << ((shift + bit) & 7));
                shift += bits;
            }
        }
        /// <summary>Samples one physical typed pixel without changing the preserved native buffer.</summary>
        static void DecodePixel(byte[] input, int source, byte[] rgba, int target, string name, int[] widths, string channels) {
            rgba[target + 3] = 255;
            if (name == "S8") { rgba[target] = input[source]; return; }
            if (name.StartsWith("Z", StringComparison.Ordinal) || name == "S8") {
                double depth = name == "S8" ? input[source] / 255d : name == "Z16" ? Read(input, source, 2) / 65535d
                    : name.StartsWith("ZF32", StringComparison.Ordinal) ? BitConverter.Int32BitsToSingle((int)Read(input, source, 4)) : (Read(input, source, 4) >> 8) / 16777215d;
                byte value = Preview(depth); rgba[target] = value; rgba[target + 1] = value; rgba[target + 2] = value; return;
            }
            if (name == "RG11B10_Float" || name == "E5BGR9_Float") {
                uint word = (uint)Read(input, source, 4);
                for (int channel = 0; channel < 3; channel++) {
                    double value = name == "E5BGR9_Float" ? (word >> (channel * 9) & 511) * Math.Pow(2, (int)(word >> 27) - 24)
                        : (double)BitConverter.UInt16BitsToHalf((ushort)((word >> (channel == 2 ? 22 : channel * 11) & (channel == 2 ? 1023u : 2047u)) << (channel == 2 ? 5 : 4)));
                    rgba[target + channel] = Preview(value);
                }
                return;
            }
            int shift = 0;
            for (int channel = 0; channel < widths.Length; channel++) {
                int bits = widths[channel]; ulong encoded = 0;
                for (int bit = 0; bit < bits; bit++) if ((input[source + (shift + bit) / 8] & (1 << ((shift + bit) & 7))) != 0) encoded |= 1UL << bit;
                double value;
                if (name.Contains("Float", StringComparison.Ordinal)) value = bits == 16 ? (double)BitConverter.UInt16BitsToHalf((ushort)encoded) : BitConverter.Int32BitsToSingle((int)encoded);
                else {
                    bool signed = name.Contains("Snorm", StringComparison.Ordinal) || name.Contains("Sint", StringComparison.Ordinal);
                    long number = signed && (encoded & (1UL << (bits - 1))) != 0 ? (long)encoded - (1L << bits) : (long)encoded;
                    value = number / (double)((1UL << (signed ? bits - 1 : bits)) - 1);
                }
                int component = Component(channels[channel]); if (component >= 0) rgba[target + component] = Preview(value);
                shift += bits;
            }
        }
        /// <summary>Quantizes unsigned eleven/ten-bit float channels from normalized source colors.</summary>
        static uint SmallFloat(byte value, int mantissaBits) {
            uint half = BitConverter.HalfToUInt16Bits((Half)(value / 255d)); int shift = 10 - mantissaBits;
            return (half + (uint)(1 << (shift - 1))) >> shift;
        }
        /// <summary>Maps channel letters to logical RGBA positions, with X treated as a constant one.</summary>
        static int Component(char channel) => channel == 'R' ? 0 : channel == 'G' ? 1 : channel == 'B' ? 2 : channel == 'A' ? 3 : -1;
        /// <summary>Clamps finite typed values for the RGBA8 view and rejects NaN rather than producing an arbitrary preview.</summary>
        public static byte Preview(double value) {
            if (double.IsNaN(value)) throw new InvalidDataException("Switch numeric texture contains NaN.");
            return (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
        }
        /// <summary>Reads a bounded native little-endian scalar.</summary>
        public static ulong Read(byte[] input, int offset, int bytes) { ulong value = 0; for (int index = 0; index < bytes; index++) value |= (ulong)input[offset + index] << (index * 8); return value; }
        /// <summary>Writes a native little-endian scalar into a previously validated element buffer.</summary>
        public static void Write(byte[] output, int offset, int bytes, ulong value) { for (int index = 0; index < bytes; index++) output[offset + index] = (byte)(value >> (index * 8)); }
    }
}
