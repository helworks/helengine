namespace helengine {
    /// <summary>Computes bounded Tegra X1 GOB and 32-byte-pitched single-level 2D storage.</summary>
    public sealed class SwitchTextureLayout {
        /// <summary>Calculates exact element dimensions and byte allocation, optionally preserving an imported GOB height.</summary>
        public SwitchTextureLayout(SwitchTextureFormat format, int width, int height, int blockHeightLog2 = -1) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width < 1 || height < 1 || width > 16384 || height > 16384) throw new ArgumentOutOfRangeException(nameof(width));
            if (blockHeightLog2 < -1 || blockHeightLog2 > 5 || (!format.BlockLinear && blockHeightLog2 > 0)) throw new ArgumentException("Invalid Tegra GOB height.");
            Format = format; Width = width; Height = height;
            Columns = (width + format.BlockWidth - 1) / format.BlockWidth;
            Rows = (height + format.BlockHeight - 1) / format.BlockHeight;
            int exponent = 0;
            if (format.BlockLinear) {
                while (exponent < 5 && (8 << exponent) < Rows) exponent++;
                if (blockHeightLog2 >= 0) exponent = blockHeightLog2;
            }
            BlockHeightLog2 = exponent;
            Pitch = Align(Columns * format.BytesPerBlock, format.BlockLinear ? 64 : 32);
            long bytes = (long)Pitch * (format.BlockLinear ? Align(Rows, 8 << exponent) : Rows);
            if (bytes > 128 * 1024 * 1024) throw new ArgumentException("Native Switch storage exceeds the 128 MiB asset budget.");
            TexelBytes = (int)bytes;
        }
        /// <summary>Gets the borrowed catalog format.</summary>
        [NativeBorrowedReturn]
        public SwitchTextureFormat Format { get; }
        /// <summary>Gets logical pixel width.</summary>
        public int Width { get; }
        /// <summary>Gets logical pixel height.</summary>
        public int Height { get; }
        /// <summary>Gets stored elements per logical row.</summary>
        public int Columns { get; }
        /// <summary>Gets stored element rows.</summary>
        public int Rows { get; }
        /// <summary>Gets the byte pitch rounded to the native row alignment.</summary>
        public int Pitch { get; }
        /// <summary>Gets the exponent of the GOB group height.</summary>
        public int BlockHeightLog2 { get; }
        /// <summary>Gets the complete padded native allocation length.</summary>
        public int TexelBytes { get; }
        /// <summary>Gets a byte's physical address, including texels that cross a sixteen-byte GOB sector.</summary>
        public int ByteOffset(int xByte, int y) {
            if (!Format.BlockLinear) return y * Pitch + xByte;
            int gobHeight = 1 << BlockHeightLog2;
            int gob = y / (8 * gobHeight) * 512 * gobHeight * (Pitch / 64)
                + xByte / 64 * 512 * gobHeight + y % (8 * gobHeight) / 8 * 512;
            return gob + xByte % 64 / 32 * 256 + y % 8 / 2 * 64 + xByte % 32 / 16 * 32 + y % 2 * 16 + xByte % 16;
        }
        /// <summary>Rounds bounded nonnegative allocations up to an integral alignment.</summary>
        static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;
    }
}
