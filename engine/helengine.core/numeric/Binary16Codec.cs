namespace helengine {
    /// <summary>Converts IEEE binary16 bit patterns without requiring a target half instruction or a hosted math library.</summary>
    public static class Binary16Codec {
        /// <summary>Rounds fixed binary64 transport directly to binary16, avoiding double rounding through binary32.</summary>
        public static ushort Encode(double value) {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            ushort sign = (ushort)((bits >> 48) & 0x8000UL);
            int exponent = (int)((bits >> 52) & 0x7ffUL);
            ulong fraction = bits & 0x000fffffffffffffUL;
            if (exponent == 0x7ff) {
                return (ushort)(sign | 0x7c00 | (fraction == 0 ? 0 : (int)(fraction >> 42) | 0x0200));
            }
            int unbiased = exponent - 1023;
            if (unbiased > 15) { return (ushort)(sign | 0x7c00); }
            if (unbiased < -25) { return sign; }
            ulong significand = fraction | 0x0010000000000000UL;
            if (unbiased < -14) {
                return (ushort)(sign | (ushort)RoundShiftEven(significand, 28 - unbiased));
            }
            int halfExponent = unbiased + 15;
            ulong rounded = RoundShiftEven(significand, 42);
            if (rounded == 0x0800UL) { halfExponent++; rounded = 0x0400UL; }
            if (halfExponent >= 31) { return (ushort)(sign | 0x7c00); }
            return (ushort)(sign | (halfExponent << 10) | (int)(rounded & 0x03ffUL));
        }

        /// <summary>Expands every binary16 value exactly into binary32, including signed zero and subnormals.</summary>
        public static float Decode(ushort bits) {
            uint sign = (uint)(bits & 0x8000) << 16;
            uint exponent = (uint)(bits >> 10) & 31U;
            uint fraction = (uint)bits & 0x03ffU;
            uint result;
            if (exponent == 31U) { result = sign | 0x7f800000U | (fraction << 13); }
            else if (exponent == 0U) {
                if (fraction == 0U) { result = sign; }
                else {
                    int unbiased = -14;
                    while ((fraction & 0x0400U) == 0U) { fraction <<= 1; unbiased--; }
                    result = sign | ((uint)(unbiased + 127) << 23) | ((fraction & 0x03ffU) << 13);
                }
            } else { result = sign | ((exponent + 112U) << 23) | (fraction << 13); }
            return BitConverter.Int32BitsToSingle(unchecked((int)result));
        }

        /// <summary>Rounds a positive significand after a right shift using IEEE nearest-even tie handling.</summary>
        static ulong RoundShiftEven(ulong value, int shift) {
            ulong rounded = value >> shift;
            ulong remainder = value & ((1UL << shift) - 1UL);
            ulong halfway = 1UL << (shift - 1);
            if (remainder > halfway || remainder == halfway && (rounded & 1UL) != 0UL) { rounded++; }
            return rounded;
        }
    }
}
