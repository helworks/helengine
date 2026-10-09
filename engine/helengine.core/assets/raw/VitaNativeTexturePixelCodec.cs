namespace helengine {
    /// <summary>Packs exact native GXM components and applies declared channel swizzles to sampled previews.</summary>
    public static class VitaNativeTexturePixelCodec {
        /// <summary>Encodes borrowed logical RGBA pixels into native component, plane or compression storage.</summary>
        public static void Encode(byte[] rgba, [NativeNoEscape] VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int offset) {
            if (layout.Format.IsBlockCompressed) { VitaNativeTextureCompressedCodec.Encode(rgba, layout, alpha, output, offset); return; }
            if (layout.Format.NumericKind == VitaNativeTextureNumericKind.Yuv) { VitaNativeTextureYuvCodec.Encode(rgba, layout, output, offset); return; }
            for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) {
                int source = (Math.Min(y, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x, layout.RealWidth - 1)) * 4;
                EncodeColor(rgba, source, layout.Format, alpha, output, offset + layout.GetPixelIndex(x, y) * layout.Format.BitsPerPixel / 8);
            }
        }
        /// <summary>Decodes native GXM pixels into logical RGBA using actual storage addressing and sampled channel swizzles.</summary>
        public static void Decode(byte[] input, int offset, [NativeNoEscape] VitaNativeTextureLayout layout, byte[] rgba) {
            if (layout.Format.IsBlockCompressed) { VitaNativeTextureCompressedCodec.Decode(input, offset, layout, rgba); return; }
            if (layout.Format.NumericKind == VitaNativeTextureNumericKind.Yuv) { VitaNativeTextureYuvCodec.Decode(input, offset, layout, rgba); return; }
            for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) DecodeColor(input, offset + layout.GetPixelIndex(x, y) * layout.Format.BitsPerPixel / 8, layout.Format, rgba, (y * layout.RealWidth + x) * 4);
        }
        /// <summary>Packs one RGBA palette entry in the physical order selected by its native P4/P8 swizzle.</summary>
        public static void EncodePaletteColor(byte[] rgba, int source, VitaNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, byte[] output, int target) {
            for (int component = 0; component < 4; component++) output[target + component] = (byte)SourceComponent(rgba, source, format, component, alpha);
        }
        /// <summary>Decodes native palette channels through the exact P4/P8 channel swizzle.</summary>
        public static void DecodePaletteColor(byte[] input, int source, VitaNativeTextureFormatDefinition format, byte[] rgba, int target) {
            for (int channel = 0; channel < 4; channel++) { int component = format.GetSwizzleComponent(channel); rgba[target + channel] = component == 4 ? (byte)0 : component == 5 ? (byte)255 : input[source + component]; }
        }
        /// <summary>Gets a deterministic physical component from independent or deliberately coupled source channels.</summary>
        public static int SourceComponent(byte[] rgba, int source, VitaNativeTextureFormatDefinition format, int component, TextureAssetAlphaPrecision alpha) {
            bool red = format.GetSwizzleComponent(0) == component; bool green = format.GetSwizzleComponent(1) == component; bool blue = format.GetSwizzleComponent(2) == component;
            if (red && green && blue) return (rgba[source] * 77 + rgba[source + 1] * 150 + rgba[source + 2] * 29 + 128) >> 8;
            if (red) return rgba[source];
            if (green) return rgba[source + 1];
            if (blue) return rgba[source + 2];
            if (format.GetSwizzleComponent(3) == component) return VitaNativeTextureNumberCodec.QuantizeAlpha(rgba[source + 3], alpha);
            return 0;
        }
        /// <summary>Packs one native direct color using physical component widths, including relocated one- and two-bit alpha.</summary>
        static void EncodeColor(byte[] rgba, int source, VitaNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, byte[] output, int target) {
            if (format.BaseFormat == 0x19) { EncodeSharedExponent(rgba, source, format, alpha, output, target); return; }
            ulong packed = 0; int shift = 0;
            for (int component = 0; component < format.ComponentCount; component++) {
                int bits = ComponentBits(format, component); int value = SourceComponent(rgba, source, format, component, alpha); ulong stored;
                if (IsFloatComponent(format, component)) stored = bits == 32 ? (uint)BitConverter.SingleToInt32Bits(value / 255.0f) : VitaNativeTextureNumberCodec.EncodeFloat(value / 255.0, bits == 16 ? 10 : bits - 5);
                else { ulong maximum = IsSignedComponent(format, component) ? (1UL << (bits - 1)) - 1 : (1UL << bits) - 1; stored = ((ulong)value * maximum + 127) / 255; }
                packed |= stored << shift; shift += bits;
            }
            VitaNativeTextureNumberCodec.Write(output, target, format.BitsPerPixel / 8, packed);
        }
        /// <summary>Unpacks native values before applying the declared SDK channel swizzle.</summary>
        static void DecodeColor(byte[] input, int source, VitaNativeTextureFormatDefinition format, byte[] rgba, int target) {
            ulong packed = VitaNativeTextureNumberCodec.Read(input, source, format.BitsPerPixel / 8);
            for (int channel = 0; channel < 4; channel++) {
                int component = format.GetSwizzleComponent(channel);
                if (component >= 4) { rgba[target + channel] = component == 4 ? (byte)0 : (byte)255; continue; }
                if (format.BaseFormat == 0x19) { int exponent = (int)(packed >> 27); int mantissa = (int)((packed >> (component * 9)) & 511); rgba[target + channel] = (byte)VitaNativeTextureNumberCodec.Preview(mantissa * VitaNativeTextureNumberCodec.PowerOfTwo(exponent - 24)); continue; }
                int shift = 0; for (int lower = 0; lower < component; lower++) shift += ComponentBits(format, lower);
                int bits = ComponentBits(format, component); ulong mask = (1UL << bits) - 1; ulong value = (packed >> shift) & mask;
                if (IsFloatComponent(format, component)) {
                    double decoded = bits == 32 ? BitConverter.Int32BitsToSingle((int)(format.BaseFormat == 0x13 ? value & 0x7fffffff : value)) : VitaNativeTextureNumberCodec.DecodeFloat((uint)value, bits == 16 ? 10 : bits - 5, bits == 16);
                    rgba[target + channel] = (byte)VitaNativeTextureNumberCodec.Preview(decoded);
                } else if (IsSignedComponent(format, component)) {
                    long signed = (value & (1UL << (bits - 1))) == 0 ? (long)value : (long)value - (1L << bits);
                    rgba[target + channel] = signed < 0 ? (byte)0 : (byte)((signed * 255 + (long)(mask >> 1) / 2) / (long)(mask >> 1));
                } else rgba[target + channel] = (byte)((value * 255 + mask / 2) / mask);
            }
        }
        /// <summary>Chooses exact packed widths from the physical word arrangement selected by the SDK swizzle.</summary>
        public static int ComponentBits(VitaNativeTextureFormatDefinition format, int component) {
            int code = format.BaseFormat;
            if (code == 2) return 4;
            if (code == 3) return component == 0 ? 2 : component == 3 ? 8 : 3;
            if (code == 4) return component == (format.Swizzle == 2 || format.Swizzle == 3 || format.Swizzle == 6 || format.Swizzle == 7 ? 0 : 3) ? 1 : 5;
            if (code == 5) return component == 1 ? 6 : 5;
            if (code == 6) return component == (format.Swizzle == 0 ? 2 : 0) ? 6 : 5;
            if (code == 14 || code == 0x9a) return component == (format.Swizzle == 2 || format.Swizzle == 3 || format.Swizzle == 6 || format.Swizzle == 7 ? 0 : 3) ? 2 : 10;
            if (code == 26) return component == (format.Swizzle == 0 ? 2 : 0) ? 10 : 11;
            return format.BitsPerPixel / format.ComponentCount;
        }
        /// <summary>Checks component signedness, retaining the unsigned six-bit component in mixed S5/S5/U6 formats.</summary>
        static bool IsSignedComponent(VitaNativeTextureFormatDefinition format, int component) { return format.NumericKind == VitaNativeTextureNumericKind.SignedNormalized && (format.BaseFormat != 6 || ComponentBits(format, component) != 6); }
        /// <summary>Checks component float interpretation while retaining native two-bit integer alpha in packed floats.</summary>
        static bool IsFloatComponent(VitaNativeTextureFormatDefinition format, int component) { return format.NumericKind == VitaNativeTextureNumericKind.Float && (format.BaseFormat != 0x9a || ComponentBits(format, component) != 2); }
        /// <summary>Packs RGB9E5 mantissas with one shared exponent using the native unsigned floating-point representation.</summary>
        static void EncodeSharedExponent(byte[] rgba, int source, VitaNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, byte[] output, int target) {
            int red = SourceComponent(rgba, source, format, 0, alpha); int green = SourceComponent(rgba, source, format, 1, alpha); int blue = SourceComponent(rgba, source, format, 2, alpha);
            double maximum = Math.Max(red, Math.Max(green, blue)) / 255.0; int exponent = 0; double scale = VitaNativeTextureNumberCodec.PowerOfTwo(-24);
            while (maximum / scale > 511.499999 && exponent < 31) { exponent++; scale *= 2; }
            uint word = (uint)exponent << 27 | (uint)Math.Floor(red / 255.0 / scale + 0.5) | (uint)Math.Floor(green / 255.0 / scale + 0.5) << 9 | (uint)Math.Floor(blue / 255.0 / scale + 0.5) << 18;
            VitaNativeTextureCodec.WriteWord(output, target, word);
        }
    }
}
