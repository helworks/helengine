namespace helengine {
    /// <summary>Converts canonical unsigned Xenos components and normalized depth without outer-container byte swapping.</summary>
    public static class Xbox360NativeTexturePixelCodec {
        /// <summary>Maps storage-equivalent expanded aliases while retaining their hardware code in the header.</summary>
        public static int BaseCode(int code) {
            if (code >= 27 && code <= 29) return code + 3;
            if (code >= 50 && code <= 56) {
                if (code == 50) return 6;
                if (code <= 53) return code - 33;
                return code == 54 ? 7 : code - 39;
            }
            if (code == 9) return 8;
            if (code == 14) return 6;
            return code;
        }
        /// <summary>Returns the significant bits of one low-to-high packed component.</summary>
        public static int ComponentBits(int code, int component) {
            if (code == 3) return component == 3 ? 1 : 5;
            if (code == 4) return component == 3 ? 0 : component == 1 ? 6 : 5;
            if (code == 5) return component == 3 ? 0 : component == 2 ? 6 : 5;
            if (code == 7) return component == 3 ? 2 : 10;
            if (code == 15) return 4;
            if (code == 16 || code == 17) return component == 3 ? 0 : component == (code == 16 ? 2 : 0) ? 10 : 11;
            if (code == 6) return 8;
            if (code == 10) return component < 2 ? 8 : 0;
            if (code == 24 || code == 25 || code == 26) return component < (code == 24 ? 1 : code == 25 ? 2 : 4) ? 16 : 0;
            return component == 0 ? 8 : 0;
        }
        /// <summary>Gets whether each component occupies a floating-point half or full word.</summary>
        public static bool IsFloat(int code) { return code >= 30 && code <= 32 || code >= 36 && code <= 38; }
        /// <summary>Gets the stored float component count.</summary>
        public static int FloatComponents(int code) { return code == 30 || code == 36 ? 1 : code == 31 || code == 37 ? 2 : 4; }
        /// <summary>Encodes one texel, using source alpha for 8_A and luminance for scalar color, the explicit 8_B alias and depth.</summary>
        public static void Encode(int hardwareCode, byte[] rgba, int source, TextureAssetAlphaPrecision alpha, byte[] data, int target) {
            int code = BaseCode(hardwareCode);
            int luminance = (rgba[source] * 77 + rgba[source + 1] * 150 + rgba[source + 2] * 29 + 128) >> 8;
            int alphaValue = alpha == TextureAssetAlphaPrecision.A2 ? (rgba[source + 3] * 3 + 127) / 255 * 85 :
                XboxNativeTexturePixelCodec.QuantizeAlpha(rgba[source + 3], alpha);
            if (hardwareCode == 8) { data[target] = (byte)alphaValue; return; }
            if (code == 22 || code == 23) {
                uint depth = code == 22 ? (uint)((long)luminance * 0xffffff / 255) : EncodeDepthFloat(luminance / 255.0f);
                XboxNativeTexturePixelCodec.WriteUInt32(data, target, depth << 8); return;
            }
            if (IsFloat(code)) {
                int count = FloatComponents(code);
                int bytes = code < 36 ? 2 : 4;
                for (int component = 0; component < count; component++) {
                    int channel = count == 1 ? luminance : component == 3 ? alphaValue : rgba[source + component];
                    float value = channel / 255.0f;
                    if (bytes == 2) XboxNativeTexturePixelCodec.WriteWord(data, target + component * bytes, EncodeHalf(value));
                    else XboxNativeTexturePixelCodec.WriteUInt32(data, target + component * bytes, (uint)BitConverter.SingleToInt32Bits(value));
                }
                return;
            }
            ulong packed = 0; int shift = 0;
            for (int component = 0; component < 4; component++) {
                int bits = ComponentBits(code, component);
                if (bits == 0) continue;
                int channel = component == 3 ? alphaValue : component == 0 && (code == 2 || code == 8 || code == 24) ? luminance : rgba[source + component];
                uint maximum = (1u << bits) - 1;
                uint value = (uint)((long)channel * maximum + 127) / 255;
                packed |= (ulong)value << shift; shift += bits;
            }
            for (int index = 0; index < (shift + 7) / 8; index++) data[target + index] = (byte)(packed >> (index * 8));
        }
        /// <summary>Decodes canonical component swizzles to an owned preview image's requested texel.</summary>
        public static void Decode(int hardwareCode, byte[] data, int source, byte[] rgba, int target) {
            if (hardwareCode == 8) { XboxNativeTexturePixelCodec.SetPixel(rgba, target, 255, 255, 255, data[source]); return; }
            int code = BaseCode(hardwareCode);
            if (code == 22 || code == 23) {
                uint depth = XboxNativeTexturePixelCodec.ReadUInt32(data, source) >> 8;
                int value = code == 22 ? (int)(((long)depth * 255 + 0x7fffff) / 0xffffff) : ToByte(DecodeDepthFloat(depth));
                XboxNativeTexturePixelCodec.SetPixel(rgba, target, value, value, value, 255); return;
            }
            XboxNativeTexturePixelCodec.SetPixel(rgba, target, 0, 0, 0, 255);
            if (IsFloat(code)) {
                int count = FloatComponents(code); int bytes = code < 36 ? 2 : 4;
                for (int component = 0; component < count; component++) {
                    float value = bytes == 2 ? DecodeHalf(XboxNativeTexturePixelCodec.ReadWord(data, source + component * bytes)) :
                        BitConverter.Int32BitsToSingle((int)XboxNativeTexturePixelCodec.ReadUInt32(data, source + component * bytes));
                    rgba[target + component] = (byte)ToByte(value);
                }
                if (count == 1) rgba[target + 1] = rgba[target + 2] = rgba[target];
            } else {
                ulong packed = 0; int totalBits = 0;
                for (int component = 0; component < 4; component++) totalBits += ComponentBits(code, component);
                for (int index = 0; index < (totalBits + 7) / 8; index++) packed |= (ulong)data[source + index] << (index * 8);
                int shift = 0;
                for (int component = 0; component < 4; component++) {
                    int bits = ComponentBits(code, component); if (bits == 0) continue;
                    uint maximum = (1u << bits) - 1;
                    rgba[target + component] = (byte)((((packed >> shift) & maximum) * 255 + maximum / 2) / maximum);
                    shift += bits;
                }
                if (code == 2 || code == 8 || code == 24) rgba[target + 1] = rgba[target + 2] = rgba[target];
            }
        }
        /// <summary>Rounds a normalized sampled float into a preview byte, clamping nonfinite and out-of-range values.</summary>
        public static int ToByte(float value) {
            if (!(value > 0)) return 0;
            if (value >= 1) return 255;
            return (int)(value * 255 + 0.5f);
        }
        /// <summary>Encodes finite nonnegative normalized values into IEEE binary16 storage.</summary>
        public static int EncodeHalf(float value) {
            uint bits = (uint)BitConverter.SingleToInt32Bits(value);
            int exponent = (int)((bits >> 23) & 255) - 127 + 15;
            uint mantissa = bits & 0x7fffff;
            if (exponent <= 0) {
                if (exponent < -10) return 0;
                return (int)(((mantissa | 0x800000) + (1u << (12 - exponent))) >> (14 - exponent));
            }
            return (int)(((uint)exponent << 10) + ((mantissa + 0x1000) >> 13));
        }
        /// <summary>Decodes IEEE binary16, including denormalized values and nonfinite bit patterns.</summary>
        public static float DecodeHalf(int word) {
            int exponent = (word >> 10) & 31; int mantissa = word & 1023;
            double value = exponent == 0 ? mantissa * PowerOfTwo(-24) : exponent == 31 ? mantissa == 0 ? double.PositiveInfinity : double.NaN :
                (1 + mantissa / 1024.0) * PowerOfTwo(exponent - 15);
            return (float)((word & 0x8000) == 0 ? value : -value);
        }
        /// <summary>Encodes unsigned Xenos twenty-bit mantissa/four-bit exponent depth with exponent bias fifteen.</summary>
        public static uint EncodeDepthFloat(float value) {
            if (value <= 0) return 0;
            uint bits = (uint)BitConverter.SingleToInt32Bits(value);
            int exponent = (int)(bits >> 23) - 112;
            if (exponent <= 0) return (uint)Math.Round(value * PowerOfTwo(34));
            return ((uint)exponent << 20) | ((bits & 0x7fffff) >> 3);
        }
        /// <summary>Decodes unsigned twenty/four depth exactly before normalized preview clamping.</summary>
        public static float DecodeDepthFloat(uint value) {
            int exponent = (int)(value >> 20); uint mantissa = value & 0xfffff;
            return exponent == 0 ? (float)(mantissa * PowerOfTwo(-34)) : (float)((1 + mantissa / 1048576.0) * PowerOfTwo(exponent - 15));
        }
        /// <summary>Constructs exact binary powers for bounded texture exponents [-34,34] without requiring a freestanding libm power function.</summary>
        static double PowerOfTwo(int exponent) {
            if (exponent < -34 || exponent > 34) throw new ArgumentOutOfRangeException(nameof(exponent));
            return BitConverter.Int32BitsToSingle((exponent + 127) << 23);
        }
    }
}
