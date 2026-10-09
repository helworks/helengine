namespace helengine {
    /// <summary>Packs and previews uncompressed NV2A channels without changing their hardware byte order or coupled-alpha semantics.</summary>
    public static class XboxNativeTexturePixelCodec {
        /// <summary>Writes one native uncompressed texel, interpreting depth input as normalized Rec.601 luma.</summary>
        public static void EncodePixel(int code, byte[] rgba, int source, TextureAssetAlphaPrecision precision, byte[] output, int target) {
            int red = rgba[source];
            int green = rgba[source + 1];
            int blue = rgba[source + 2];
            int alpha = QuantizeAlpha(rgba[source + 3], precision);
            int intensity = GetIntensity(rgba, source);
            switch (code) {
                case 0x00: case 0x13: case 0x01: case 0x1b:
                    output[target] = (byte)intensity;
                    break;
                case 0x19: case 0x1f:
                    output[target] = (byte)alpha;
                    break;
                case 0x1a: case 0x20:
                    output[target] = (byte)intensity;
                    output[target + 1] = (byte)alpha;
                    break;
                case 0x02: case 0x10: case 0x03: case 0x1c:
                    WriteWord(output, target, ((code == 0x03 || code == 0x1c ? 1 : alpha >> 7) << 15)
                        | ((red >> 3) << 10) | ((green >> 3) << 5) | (blue >> 3));
                    break;
                case 0x04: case 0x1d:
                    WriteWord(output, target, ((alpha >> 4) << 12) | ((red >> 4) << 8) | ((green >> 4) << 4) | (blue >> 4));
                    break;
                case 0x05: case 0x11:
                    WriteWord(output, target, PackRgb565(red, green, blue));
                    break;
                case 0x06: case 0x12: case 0x07: case 0x1e:
                    SetPixel(output, target, blue, green, red, code == 0x07 || code == 0x1e ? 255 : alpha);
                    break;
                case 0x3a: case 0x3f:
                    SetPixel(output, target, red, green, blue, alpha);
                    break;
                case 0x3b: case 0x40:
                    SetPixel(output, target, alpha, red, green, blue);
                    break;
                case 0x3c: case 0x41:
                    SetPixel(output, target, alpha, blue, green, red);
                    break;
                case 0x17: case 0x28:
                    output[target] = (byte)blue;
                    output[target + 1] = (byte)green;
                    break;
                case 0x16: case 0x29:
                    output[target] = (byte)blue;
                    output[target + 1] = (byte)red;
                    break;
                case 0x27:
                    WriteWord(output, target, ((red >> 2) << 10) | (((green >> 3) ^ 16) << 5) | ((blue >> 3) ^ 16));
                    break;
                case 0x2c: case 0x30: case 0x35:
                    WriteWord(output, target, intensity * 257);
                    break;
                case 0x2e:
                    WriteUInt32(output, target, (uint)(intensity * 65793) << 8);
                    break;
                case 0x31:
                    // NV2A float16 stores a positive float's exponent and mantissa with a bias of 0x3c000000,
                    // unlike IEEE half. Encode normalized depth 0..1; white depth is the native word 0x7000.
                    int bits = BitConverter.SingleToInt32Bits((float)(intensity / 255.0));
                    WriteWord(output, target, intensity == 0 ? 0 : Math.Max(0, (bits - 0x3c000000 + 1024) >> 11));
                    break;
                default:
                    throw new NotSupportedException("This color code requires indexed, YUV or block conversion.");
            }
        }

        /// <summary>Reads one hardware texel into unsigned RGBA preview bytes; negative signed bump components clamp to zero.</summary>
        public static void DecodePixel(int code, byte[] input, int source, byte[] rgba, int target) {
            int word;
            int intensity;
            switch (code) {
                case 0x00: case 0x13: case 0x01: case 0x1b:
                    intensity = input[source];
                    SetPixel(rgba, target, intensity, intensity, intensity, code == 0x01 || code == 0x1b ? intensity : 255);
                    break;
                case 0x19: case 0x1f:
                    SetPixel(rgba, target, 255, 255, 255, input[source]);
                    break;
                case 0x1a: case 0x20:
                    intensity = input[source];
                    SetPixel(rgba, target, intensity, intensity, intensity, input[source + 1]);
                    break;
                case 0x02: case 0x10: case 0x03: case 0x1c:
                    word = ReadWord(input, source);
                    SetPixel(rgba, target, ExpandBits((word >> 10) & 31, 5), ExpandBits((word >> 5) & 31, 5),
                        ExpandBits(word & 31, 5), code == 0x03 || code == 0x1c || (word & 0x8000) != 0 ? 255 : 0);
                    break;
                case 0x04: case 0x1d:
                    word = ReadWord(input, source);
                    SetPixel(rgba, target, ((word >> 8) & 15) * 17, ((word >> 4) & 15) * 17,
                        (word & 15) * 17, (word >> 12) * 17);
                    break;
                case 0x05: case 0x11:
                    DecodeRgb565(ReadWord(input, source), rgba, target);
                    break;
                case 0x06: case 0x12: case 0x07: case 0x1e:
                    SetPixel(rgba, target, input[source + 2], input[source + 1], input[source],
                        code == 0x07 || code == 0x1e ? 255 : input[source + 3]);
                    break;
                case 0x3a: case 0x3f:
                    SetPixel(rgba, target, input[source], input[source + 1], input[source + 2], input[source + 3]);
                    break;
                case 0x3b: case 0x40:
                    SetPixel(rgba, target, input[source + 1], input[source + 2], input[source + 3], input[source]);
                    break;
                case 0x3c: case 0x41:
                    SetPixel(rgba, target, input[source + 3], input[source + 2], input[source + 1], input[source]);
                    break;
                case 0x17: case 0x28:
                    SetPixel(rgba, target, input[source], input[source + 1], input[source], input[source + 1]);
                    break;
                case 0x16: case 0x29:
                    SetPixel(rgba, target, input[source + 1], input[source], input[source], input[source + 1]);
                    break;
                case 0x27:
                    word = ReadWord(input, source);
                    SetPixel(rgba, target, ExpandBits(word >> 10, 6), DecodeSignedFive((word >> 5) & 31), DecodeSignedFive(word & 31), 255);
                    break;
                case 0x2c: case 0x30: case 0x35:
                    intensity = (ReadWord(input, source) * 255 + 32767) / 65535;
                    SetPixel(rgba, target, intensity, intensity, intensity, 255);
                    break;
                case 0x2e:
                    intensity = (int)(((ReadUInt32(input, source) >> 8) * 255ul + 8388607ul) / 16777215ul);
                    SetPixel(rgba, target, intensity, intensity, intensity, 255);
                    break;
                case 0x31:
                    word = ReadWord(input, source);
                    float depth = word == 0 ? 0 : BitConverter.Int32BitsToSingle((word << 11) + 0x3c000000);
                    intensity = ClampByte((int)(depth * 255.0 + 0.5));
                    SetPixel(rgba, target, intensity, intensity, intensity, 255);
                    break;
                default:
                    throw new NotSupportedException("This color code requires indexed, YUV or block conversion.");
            }
        }

        /// <summary>Encodes a complete row of YUY2 or UYVY pairs using BT.601 limited-range YUV; odd rows repeat their final texel.</summary>
        public static void EncodeYuvRow(int code, byte[] rgba, int width, int row, byte[] output, int target) {
            for (int x = 0; x < width; x += 2) {
                int first = (row * width + x) * 4;
                int second = (row * width + Math.Min(x + 1, width - 1)) * 4;
                int y0 = GetYuvLuma(rgba, first);
                int y1 = GetYuvLuma(rgba, second);
                int u = (GetYuvChroma(rgba, first, false) + GetYuvChroma(rgba, second, false) + 1) / 2;
                int v = (GetYuvChroma(rgba, first, true) + GetYuvChroma(rgba, second, true) + 1) / 2;
                if (code == 0x24) SetPixel(output, target + x * 2, y0, u, y1, v);
                else SetPixel(output, target + x * 2, u, y0, v, y1);
            }
        }

        /// <summary>Decodes a pixel of a packed limited-range YUV422 pair with the integer conversion used by xemu.</summary>
        public static void DecodeYuvPixel(int code, byte[] input, int rowStart, int x, byte[] output, int target) {
            int pair = rowStart + (x / 2) * 4;
            int luma = input[pair + (code == 0x24 ? (x % 2) * 2 : 1 + (x % 2) * 2)] - 16;
            int u = input[pair + (code == 0x24 ? 1 : 0)] - 128;
            int v = input[pair + (code == 0x24 ? 3 : 2)] - 128;
            SetPixel(output, target, ClampByte((298 * luma + 409 * v + 128) >> 8),
                ClampByte((298 * luma - 100 * u - 208 * v + 128) >> 8),
                ClampByte((298 * luma + 516 * u + 128) >> 8), 255);
        }

        /// <summary>Quantizes independent alpha without changing the source buffer.</summary>
        public static int QuantizeAlpha(int alpha, TextureAssetAlphaPrecision precision) {
            switch (precision) {
                case TextureAssetAlphaPrecision.Opaque: return 255;
                case TextureAssetAlphaPrecision.Binary: return alpha >= 128 ? 255 : 0;
                case TextureAssetAlphaPrecision.A4: return (alpha >> 4) * 17;
                case TextureAssetAlphaPrecision.A8: return alpha;
                default: throw new ArgumentException("Unknown alpha precision.");
            }
        }

        /// <summary>Computes Rec.601 intensity rounded to the nearest byte from straight RGBA input.</summary>
        public static int GetIntensity(byte[] rgba, int offset) {
            return (299 * rgba[offset] + 587 * rgba[offset + 1] + 114 * rgba[offset + 2] + 500) / 1000;
        }

        /// <summary>Packs truncating RGB channels into a standard little-endian RGB565 endpoint.</summary>
        public static int PackRgb565(int red, int green, int blue) {
            return ((red >> 3) << 11) | ((green >> 2) << 5) | (blue >> 3);
        }

        /// <summary>Expands a RGB565 endpoint by bit replication and gives it opaque alpha.</summary>
        public static void DecodeRgb565(int word, byte[] rgba, int offset) {
            SetPixel(rgba, offset, ExpandBits(word >> 11, 5), ExpandBits((word >> 5) & 63, 6), ExpandBits(word & 31, 5), 255);
        }

        /// <summary>Writes four already-clamped bytes to one RGBA pixel or packed four-byte storage element.</summary>
        public static void SetPixel(byte[] pixels, int offset, int red, int green, int blue, int alpha) {
            pixels[offset] = (byte)red;
            pixels[offset + 1] = (byte)green;
            pixels[offset + 2] = (byte)blue;
            pixels[offset + 3] = (byte)alpha;
        }

        /// <summary>Reads one little-endian packed word without depending on HELE record metadata endianness.</summary>
        public static int ReadWord(byte[] bytes, int offset) {
            return bytes[offset] | (bytes[offset + 1] << 8);
        }

        /// <summary>Writes one little-endian packed word independently of HELE record metadata endianness.</summary>
        public static void WriteWord(byte[] bytes, int offset, int word) {
            bytes[offset] = (byte)word;
            bytes[offset + 1] = (byte)(word >> 8);
        }

        /// <summary>Reads one unsigned little-endian header or native channel value.</summary>
        public static uint ReadUInt32(byte[] bytes, int offset) {
            return (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);
        }

        /// <summary>Writes one unsigned little-endian header or native channel value.</summary>
        public static void WriteUInt32(byte[] bytes, int offset, uint word) {
            for (int index = 0; index < 4; index++) bytes[offset + index] = (byte)(word >> (index * 8));
        }

        /// <summary>Expands a five- or six-bit normalized color channel using hardware-style bit replication.</summary>
        static int ExpandBits(int value, int bits) {
            return bits == 5 ? (value << 3) | (value >> 2) : (value << 2) | (value >> 4);
        }

        /// <summary>Converts a signed five-bit bump sample to the unsigned preview range, clamping negative values like color output.</summary>
        static int DecodeSignedFive(int value) {
            int signedComponent = value < 16 ? value : value - 32;
            return ClampByte((signedComponent * 255 + 7) / 15);
        }

        /// <summary>Clamps conversion output to the unsigned channel range.</summary>
        static int ClampByte(int value) {
            return Math.Min(255, Math.Max(0, value));
        }

        /// <summary>Computes BT.601 limited-range luma from one RGB texel.</summary>
        static int GetYuvLuma(byte[] rgba, int offset) {
            return ClampByte(((66 * rgba[offset] + 129 * rgba[offset + 1] + 25 * rgba[offset + 2] + 128) >> 8) + 16);
        }

        /// <summary>Computes BT.601 limited-range chroma from one RGB texel.</summary>
        static int GetYuvChroma(byte[] rgba, int offset, bool redChroma) {
            int value = redChroma ? 112 * rgba[offset] - 94 * rgba[offset + 1] - 18 * rgba[offset + 2]
                : -38 * rgba[offset] - 74 * rgba[offset + 1] + 112 * rgba[offset + 2];
            return ClampByte(((value + 128) >> 8) + 128);
        }
    }
}
