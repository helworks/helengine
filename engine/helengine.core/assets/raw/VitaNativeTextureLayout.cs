namespace helengine {
    /// <summary>Computes canonical scanline, Morton and tile allocations while preserving logical dimensions.</summary>
    public sealed class VitaNativeTextureLayout {
        /// <summary>Constructs exact native storage for a supported single-level two-dimensional selection.</summary>
        public VitaNativeTextureLayout(VitaNativeTextureFormatDefinition format, int width, int height, VitaNativeTextureLayoutType type) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new ArgumentOutOfRangeException("GXM dimensions must be within 1..4096.");
            if (!format.SupportsLayout(type)) throw new ArgumentException("GXM format does not support this canonical storage layout.");
            Format = format; RealWidth = width; RealHeight = height; LayoutType = type;
            if (type == VitaNativeTextureLayoutType.Linear) {
                StorageWidth = Align(width, 8); StorageHeight = Align(height, format.BlockHeight);
                if (format.BaseFormat == 0x90 || format.BaseFormat == 0x91) StorageHeight = Align(height, 2);
            } else if (type == VitaNativeTextureLayoutType.Tiled) { StorageWidth = Align(width, 32); StorageHeight = Align(height, 32); }
            else {
                StorageWidth = PowerOfTwo(Math.Max(width, format.IsBlockCompressed ? format.IsPvrtc ? format.BitsPerPixel == 2 ? 16 : 8 : 4 : 1));
                StorageHeight = PowerOfTwo(Math.Max(height, format.IsPvrtc ? 8 : format.BlockHeight));
                if (type == VitaNativeTextureLayoutType.Swizzled && (StorageWidth != width || StorageHeight != height)) throw new ArgumentException("GXM power-of-two swizzled dimensions must match storage.");
            }
            TexelLength = checked((StorageWidth * StorageHeight * format.BitsPerPixel + 7) / 8);
            PitchBytes = type == VitaNativeTextureLayoutType.Linear ? format.IsBlockCompressed ? StorageWidth / format.BlockWidth * format.BytesPerBlock : format.BaseFormat == 0x90 || format.BaseFormat == 0x91 ? StorageWidth : StorageWidth * format.BitsPerPixel / 8 : 0;
            PaletteEntryCount = format.PaletteEntryCount; PaletteLength = PaletteEntryCount * 4;
        }
        /// <summary>Gets the borrowed format including exact hardware swizzle.</summary>
        [NativeBorrowedReturn] public VitaNativeTextureFormatDefinition Format { get; }
        /// <summary>Gets the real horizontal sampling extent.</summary>
        public int RealWidth { get; }
        /// <summary>Gets the real vertical sampling extent.</summary>
        public int RealHeight { get; }
        /// <summary>Gets the canonical SDK texture-type word.</summary>
        public VitaNativeTextureLayoutType LayoutType { get; }
        /// <summary>Gets horizontal allocation extent after native alignment.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets vertical allocation extent including complete compression blocks and planes.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets scanline or block-row bytes, zero for other layouts.</summary>
        public int PitchBytes { get; }
        /// <summary>Gets complete native texel or plane bytes.</summary>
        public int TexelLength { get; }
        /// <summary>Gets full native palette capacity.</summary>
        public int PaletteEntryCount { get; }
        /// <summary>Gets native ABGR32 palette bytes.</summary>
        public int PaletteLength { get; }
        /// <summary>Gets the native byte address of a compressed block.</summary>
        public int GetBlockOffset(int x, int y) {
            int width = StorageWidth / Format.BlockWidth; int height = StorageHeight / Format.BlockHeight;
            return (LayoutType == VitaNativeTextureLayoutType.Linear ? y * width + x : MortonIndex(x, y, width, height)) * Format.BytesPerBlock;
        }
        /// <summary>Gets the physical texel index for component or palette addressing.</summary>
        public int GetPixelIndex(int x, int y) {
            if (LayoutType == VitaNativeTextureLayoutType.Linear) return y * StorageWidth + x;
            if (LayoutType == VitaNativeTextureLayoutType.Tiled) return ((y / 32) * (StorageWidth / 32) + x / 32) * 1024 + (y % 32) * 32 + x % 32;
            return MortonIndex(x, y, StorageWidth, StorageHeight);
        }
        /// <summary>Interleaves Y before X and appends the remaining long-axis bits.</summary>
        public static int MortonIndex(int x, int y, int width, int height) {
            int index = 0; int bit = 1; int target = 1;
            while (bit < width || bit < height) {
                if (bit < height) { if ((y & bit) != 0) index |= target; target <<= 1; }
                if (bit < width) { if ((x & bit) != 0) index |= target; target <<= 1; }
                bit <<= 1;
            }
            return index;
        }
        /// <summary>Rounds positive extents to a row or tile boundary.</summary>
        static int Align(int value, int alignment) { return (value + alignment - 1) / alignment * alignment; }
        /// <summary>Rounds supported extents to powers of two without floating-point logarithms.</summary>
        static int PowerOfTwo(int value) { int result = 1; while (result < value) result <<= 1; return result; }
    }
}
