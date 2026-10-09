namespace helengine {
    /// <summary>Packs exact native GX2 scalar and packed components and reconstructs canonical sampled RGBA channels.</summary>
    public static class WiiUNativeTexturePixelCodec {
        /// <summary>Encodes borrowed logical pixels into edge-clamped scalar or standard BC storage.</summary>
        public static void Encode(byte[] rgba, WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start) {
            if (layout.Format.IsBlockCompressed) { WiiUNativeTextureBcCodec.Encode(rgba, layout, alpha, output, start); return; }
            WiiUNativeTextureFormatDefinition format = layout.Format;
            for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) {
                int source = (Math.Min(y, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x, layout.RealWidth - 1)) * 4; int target = start + y * layout.PitchBytes + x * format.BitsPerPixel / 8;
                for (int component = 0; component < format.ComponentCount; component++) {
                    int value = SourceComponent(rgba, source, format, component, alpha); int bits = GetComponentBits(format, component); uint stored;
                    if (format.NumericKind == WiiUNativeTextureNumericKind.Float) stored = bits == 32 ? (uint)BitConverter.SingleToInt32Bits(value / 255.0f) : VitaNativeTextureNumberCodec.EncodeFloat(value / 255.0, bits == 16 ? 10 : bits - 5);
                    else { ulong maximum = format.NumericKind == WiiUNativeTextureNumericKind.SignedNormalized ? (1UL << (bits - 1)) - 1 : (1UL << bits) - 1; stored = (uint)(((ulong)value * maximum + 127) / 255); }
                    WriteComponent(output, target, GetComponentShift(format, component), bits, stored);
                }
            }
        }
        /// <summary>Samples real dimensions through native channel selectors and sRGB conversion, without interpreting shader-only data.</summary>
        public static void Decode(byte[] input, int start, WiiUNativeTextureLayout layout, byte[] rgba) {
            if (layout.Format.IsBlockCompressed) { WiiUNativeTextureBcCodec.Decode(input, start, layout, rgba); return; }
            WiiUNativeTextureFormatDefinition format = layout.Format;
            for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) {
                int source = start + y * layout.PitchBytes + x * format.BitsPerPixel / 8; int target = (y * layout.RealWidth + x) * 4;
                for (int channel = 0; channel < 4; channel++) {
                    int component = format.GetComponent(channel); if (component >= 4) { rgba[target + channel] = component == 4 ? (byte)0 : (byte)255; continue; }
                    int bits = GetComponentBits(format, component); uint value = ReadComponent(input, source, GetComponentShift(format, component), bits); int sample;
                    if (format.NumericKind == WiiUNativeTextureNumericKind.Float) sample = VitaNativeTextureNumberCodec.Preview(bits == 32 ? BitConverter.Int32BitsToSingle((int)value) : VitaNativeTextureNumberCodec.DecodeFloat(value, bits == 16 ? 10 : bits - 5, bits == 16));
                    else if (format.NumericKind == WiiUNativeTextureNumericKind.SignedNormalized) { long signed = (value & (1u << (bits - 1))) == 0 ? value : (long)value - (1L << bits); long maximum = (1L << (bits - 1)) - 1; sample = signed <= 0 ? 0 : (int)((signed * 255 + maximum / 2) / maximum); }
                    else { ulong maximum = (1UL << bits) - 1; sample = (int)(((ulong)value * 255 + maximum / 2) / maximum); }
                    rgba[target + channel] = format.NumericKind == WiiUNativeTextureNumericKind.Srgb && channel < 3 ? WiiUNativeTextureSrgbCodec.Decode(sample) : (byte)sample;
                }
            }
        }
        /// <summary>Gets physical source data by inverting canonical channel selectors, with independent alpha and sRGB policies.</summary>
        public static int SourceComponent(byte[] rgba, int source, WiiUNativeTextureFormatDefinition format, int component, TextureAssetAlphaPrecision alpha) {
            for (int channel = 0; channel < 4; channel++) if (format.GetComponent(channel) == component) {
                if (channel == 3) return VitaNativeTextureNumberCodec.QuantizeAlpha(rgba[source + 3], alpha);
                return format.NumericKind == WiiUNativeTextureNumericKind.Srgb ? WiiUNativeTextureSrgbCodec.Encode(rgba[source + channel]) : rgba[source + channel];
            }
            return 0;
        }
        /// <summary>Gets exact physical channel precision, including reverse-mapped native one- and two-bit alpha.</summary>
        public static int GetComponentBits(WiiUNativeTextureFormatDefinition format, int component) {
            int code = format.BaseFormat;
            if (code == 2 || code == 11) return 4;
            if (code == 8) return component == 1 ? 6 : 5;
            if (code == 10) return component == 3 ? 1 : 5;
            if (code == 12) return component == 0 ? 1 : 5;
            if (code == 17) return component == 0 ? 8 : 24;
            if (code == 25) return component == 3 ? 2 : 10;
            if (code == 27) return component == 0 ? 2 : 10;
            if (code == 22) return component == 2 ? 10 : 11;
            return format.BitsPerPixel / format.ComponentCount;
        }
        /// <summary>Gets physical packed bit offset; native words are little-endian regardless of the outer container.</summary>
        public static int GetComponentShift(WiiUNativeTextureFormatDefinition format, int component) { int shift = 0; for (int index = 0; index < component; index++) shift += GetComponentBits(format, index); return shift; }
        /// <summary>Reads one component of up to 32 bits without truncating native 128-bit vector formats.</summary>
        static uint ReadComponent(byte[] input, int source, int shift, int bits) { uint value = 0; for (int bit = 0; bit < bits; bit++) if ((input[source + (shift + bit) / 8] & (1 << ((shift + bit) % 8))) != 0) value |= 1u << bit; return value; }
        /// <summary>Writes one bounded component into initially zeroed native storage.</summary>
        static void WriteComponent(byte[] output, int target, int shift, int bits, uint value) { for (int bit = 0; bit < bits; bit++) if ((value & (1u << bit)) != 0) output[target + (shift + bit) / 8] |= (byte)(1 << ((shift + bit) % 8)); }
    }
}
