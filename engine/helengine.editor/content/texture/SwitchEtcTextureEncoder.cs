namespace helengine.editor {
    /// <summary>Fits ETC1-compatible RGB blocks, ETC2 punch-through blocks and EAC alpha/channel blocks for native Switch cooking.</summary>
    public static class SwitchEtcTextureEncoder {
        /// <summary>Contains EAC table-zero's eight signed endpoint modifiers in native selector order.</summary>
        static readonly int[] Modifiers = new[] { -3, -6, -9, -15, 2, 5, 8, 14 };
        /// <summary>Encodes edge-clamped native blocks; the decoder independently supports the full ETC2 mode set.</summary>
        public static byte[] Encode(byte[] rgba, int width, int height, SwitchTextureFormat format) {
            int columns = (width + 3) / 4, rows = (height + 3) / 4;
            byte[] result = new byte[columns * rows * format.BytesPerBlock], block = new byte[64], etc = new byte[32];
            Nintendo3DsTextureLayout rgbLayout = new Nintendo3DsTextureLayout(Nintendo3DsTextureFormatCatalog.GetFormat(12), 4, 4);
            for (int by = 0; by < rows; by++) for (int bx = 0; bx < columns; bx++) {
                for (int pixel = 0; pixel < 16; pixel++) {
                    int source = (Math.Min(by * 4 + pixel / 4, height - 1) * width + Math.Min(bx * 4 + pixel % 4, width - 1)) * 4;
                    Array.Copy(rgba, source, block, pixel * 4, 4);
                }
                int target = (by * columns + bx) * format.BytesPerBlock;
                if (format.Name.StartsWith("R_", StringComparison.Ordinal) || format.Name.StartsWith("RG_", StringComparison.Ordinal)) {
                    Eac(block, 0, format.Name.Contains("Snorm", StringComparison.Ordinal), false, result, target);
                    if (format.Name.StartsWith("RG_", StringComparison.Ordinal)) Eac(block, 1, format.Name.Contains("Snorm", StringComparison.Ordinal), false, result, target + 8);
                } else {
                    if (format.Name.StartsWith("RGBA_", StringComparison.Ordinal)) { Eac(block, 3, false, true, result, target); target += 8; }
                    if (format.Name.Contains("PTA", StringComparison.Ordinal)) PunchThrough(block, result, target);
                    else {
                        Nintendo3DsEtcTextureCodec.Encode(block, 4, 4, rgbLayout, TextureAssetAlphaPrecision.Opaque, etc, 0);
                        for (int index = 0; index < 8; index++) result[target + index] = etc[7 - index];
                    }
                }
            }
            return result;
        }
        /// <summary>Fits a native EAC table-zero block by searching local base values, all multipliers and per-pixel selectors.</summary>
        static void Eac(byte[] rgba, int component, bool signed, bool alpha, byte[] output, int target) {
            int sum = 0; for (int pixel = 0; pixel < 16; pixel++) sum += rgba[pixel * 4 + component];
            int mean = (sum + 8) / 16, center = signed ? (mean * 127 + 127) / 255 : mean;
            long bestError = long.MaxValue; ulong bestSelectors = 0; int bestBase = center, bestMultiplier = 0;
            for (int endpoint = Math.Max(signed ? -127 : 0, center - 4); endpoint <= Math.Min(signed ? 127 : 255, center + 4); endpoint++) {
                for (int multiplier = 0; multiplier < 16; multiplier++) {
                    long error = 0; ulong selectors = 0;
                    for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) {
                        int sample = rgba[(y * 4 + x) * 4 + component], selected = 0, distance = int.MaxValue;
                        int desired = alpha ? sample : (sample * (signed ? 1023 : 2047) + 127) / 255;
                        for (int selector = 0; selector < 8; selector++) {
                            int value = alpha ? Math.Clamp(endpoint + Modifiers[selector] * multiplier, 0, 255)
                                : Math.Clamp(endpoint * 8 + (signed ? 0 : 4) + Modifiers[selector] * (multiplier == 0 ? 1 : multiplier * 8), signed ? -1023 : 0, signed ? 1023 : 2047);
                            int delta = desired - value; int squared = delta * delta;
                            if (squared < distance) { distance = squared; selected = selector; }
                        }
                        error += distance; selectors |= (ulong)selected << (45 - (x * 4 + y) * 3);
                    }
                    if (error < bestError) { bestError = error; bestSelectors = selectors; bestBase = endpoint; bestMultiplier = multiplier; }
                }
            }
            output[target] = (byte)bestBase; output[target + 1] = (byte)(bestMultiplier << 4);
            for (int index = 0; index < 6; index++) output[target + 2 + index] = (byte)(bestSelectors >> ((5 - index) * 8));
        }
        /// <summary>Creates a legal differential-mode ETC2 punch-through block with a fitted flat color and exact binary coverage.</summary>
        static void PunchThrough(byte[] rgba, byte[] output, int target) {
            int[] sums = new int[3]; int count = 0; bool opaque = true;
            for (int pixel = 0; pixel < 16; pixel++) {
                if (rgba[pixel * 4 + 3] < 128) { opaque = false; continue; }
                count++; for (int channel = 0; channel < 3; channel++) sums[channel] += rgba[pixel * 4 + channel];
            }
            ulong word = opaque ? 1UL << 33 : 0;
            for (int channel = 0; channel < 3; channel++) {
                int mean = count == 0 ? 0 : sums[channel] / count;
                int endpoint = Math.Clamp((int)Math.Round((mean - (opaque ? 2 : 0)) * 31 / 255d), 0, 31);
                word |= (ulong)endpoint << (59 - channel * 8);
            }
            for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) if (rgba[(y * 4 + x) * 4 + 3] < 128) word |= 1UL << (16 + x * 4 + y);
            for (int index = 0; index < 8; index++) output[target + index] = (byte)(word >> ((7 - index) * 8));
        }
    }
}
