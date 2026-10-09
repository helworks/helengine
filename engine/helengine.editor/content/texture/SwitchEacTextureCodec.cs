namespace helengine.editor {
    /// <summary>Decodes exact eleven-bit EAC channels using the Khronos tables and native selector order.</summary>
    public static class SwitchEacTextureCodec {
        /// <summary>Contains the sixteen standardized EAC tables, with eight selector entries per table.</summary>
        static readonly int[] Modifiers = new[] {
            -3, -6, -9, -15, 2, 5, 8, 14, -3, -7, -10, -13, 2, 6, 9, 12,
            -2, -5, -8, -13, 1, 4, 7, 12, -2, -4, -6, -13, 1, 3, 5, 12,
            -3, -6, -8, -12, 2, 5, 7, 11, -3, -7, -9, -11, 2, 6, 8, 10,
            -4, -7, -8, -11, 3, 6, 7, 10, -3, -5, -8, -11, 2, 4, 7, 10,
            -2, -6, -8, -10, 1, 5, 7, 9, -2, -5, -8, -10, 1, 4, 7, 9,
            -2, -4, -8, -10, 1, 3, 7, 9, -2, -5, -7, -10, 1, 4, 6, 9,
            -3, -4, -7, -10, 2, 3, 6, 9, -1, -2, -3, -10, 0, 1, 2, 9,
            -4, -6, -8, -9, 3, 5, 7, 8, -3, -5, -7, -9, 2, 4, 6, 8
        };
        /// <summary>Decodes one or two channels, clamping signed negative values only in the RGBA8 CPU view.</summary>
        public static byte[] Decode(byte[] blocks, int width, int height, bool signed, bool twoChannels) {
            byte[] output = new byte[SwitchTextureCodec.PreviewBytes(width, height)];
            int columns = (width + 3) / 4, rows = (height + 3) / 4, channels = twoChannels ? 2 : 1;
            if (blocks == null || blocks.Length != columns * rows * channels * 8) throw new InvalidDataException("Invalid EAC storage length.");
            for (int index = 3; index < output.Length; index += 4) output[index] = 255;
            for (int by = 0; by < rows; by++) for (int bx = 0; bx < columns; bx++) for (int channel = 0; channel < channels; channel++) {
                int offset = ((by * columns + bx) * channels + channel) * 8;
                int endpoint = signed ? (sbyte)blocks[offset] : blocks[offset];
                if (signed && endpoint == -128) throw new InvalidDataException("Signed EAC base -128 is reserved.");
                int multiplier = blocks[offset + 1] >> 4, table = (blocks[offset + 1] & 15) * 8;
                ulong selectors = 0; for (int index = 0; index < 6; index++) selectors = selectors << 8 | blocks[offset + 2 + index];
                for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) {
                    int px = bx * 4 + x, py = by * 4 + y; if (px >= width || py >= height) continue;
                    int selector = (int)(selectors >> (45 - (x * 4 + y) * 3) & 7);
                    int value = endpoint * 8 + (signed ? 0 : 4) + Modifiers[table + selector] * (multiplier == 0 ? 1 : multiplier * 8);
                    output[(py * width + px) * 4 + channel] = SwitchTexturePixelCodec.Preview(Math.Clamp(value, signed ? -1023 : 0, signed ? 1023 : 2047) / (signed ? 1023d : 2047d));
                }
            }
            return output;
        }
    }
}
