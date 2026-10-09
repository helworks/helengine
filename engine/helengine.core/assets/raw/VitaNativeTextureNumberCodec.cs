namespace helengine {
    /// <summary>Packs native normalized and binary floating-point components using portable arithmetic and bit operations.</summary>
    public static class VitaNativeTextureNumberCodec {
        /// <summary>Encodes nonnegative normalized source coverage into IEEE half or unsigned packed-float bits.</summary>
        public static uint EncodeFloat(double value, int mantissaBits) {
            if (value <= 0) return 0;
            int exponent = -14; double scale = PowerOfTwo(-14);
            if (value < scale) return (uint)Math.Floor(value / scale * (1 << mantissaBits) + 0.5);
            while (value >= scale * 2 && exponent < 15) { exponent++; scale *= 2; }
            int mantissa = (int)Math.Floor((value / scale - 1) * (1 << mantissaBits) + 0.5);
            if (mantissa == 1 << mantissaBits) { mantissa = 0; exponent++; }
            return (uint)((exponent + 15) << mantissaBits | mantissa);
        }
        /// <summary>Decodes IEEE half or unsigned packed-float values, retaining sign, subnormals and nonfinite behavior.</summary>
        public static double DecodeFloat(uint bits, int mantissaBits, bool signed) {
            int exponent = (int)((bits >> mantissaBits) & 31); int mantissa = (int)(bits & (uint)((1 << mantissaBits) - 1));
            bool negative = signed && (bits & (1U << (mantissaBits + 5))) != 0;
            double value = exponent == 0 ? mantissa * PowerOfTwo(-14 - mantissaBits) : exponent == 31 ? mantissa == 0 ? double.PositiveInfinity : double.NaN : (1.0 + mantissa / (double)(1 << mantissaBits)) * PowerOfTwo(exponent - 15);
            return negative ? -value : value;
        }
        /// <summary>Converts a binary floating-point sample into honest clamped RGBA preview intensity.</summary>
        public static int Preview(double value) { return double.IsNaN(value) || value <= 0 ? 0 : value >= 1 ? 255 : (int)Math.Floor(value * 255 + 0.5); }
        /// <summary>Computes exact bounded powers of two without a libm dependency.</summary>
        public static double PowerOfTwo(int exponent) { return BitConverter.Int32BitsToSingle((exponent + 127) << 23); }
        /// <summary>Quantizes source alpha only when it occupies an independent sampled native component.</summary>
        public static int QuantizeAlpha(int alpha, TextureAssetAlphaPrecision precision) {
            if (precision == TextureAssetAlphaPrecision.Opaque) return 255;
            if (precision == TextureAssetAlphaPrecision.Binary) return alpha >= 128 ? 255 : 0;
            if (precision == TextureAssetAlphaPrecision.A4) return ((alpha * 15 + 127) / 255) * 17;
            if (precision == TextureAssetAlphaPrecision.A2) return ((alpha * 3 + 127) / 255) * 85;
            return alpha;
        }
        /// <summary>Reads a little-endian component of one through eight bytes.</summary>
        public static ulong Read(byte[] input, int offset, int bytes) {
            ulong value = 0; for (int index = 0; index < bytes; index++) value |= (ulong)input[offset + index] << (index * 8); return value;
        }
        /// <summary>Writes a little-endian component of one through eight bytes.</summary>
        public static void Write(byte[] output, int offset, int bytes, ulong value) { for (int index = 0; index < bytes; index++) output[offset + index] = (byte)(value >> (index * 8)); }
    }
}
