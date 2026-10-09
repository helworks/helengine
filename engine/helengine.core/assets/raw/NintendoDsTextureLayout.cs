namespace helengine {
    /// <summary>Calculates checked DS storage extents, texel sizes, descriptor sizes and palette budgets.</summary>
    public sealed class NintendoDsTextureLayout {
        /// <summary>Initializes a native layout with independent power-of-two axes and minimum eight.</summary>
        public NintendoDsTextureLayout(NintendoDsTextureFormat format, int width, int height) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new ArgumentOutOfRangeException(nameof(width), "DS texture extents must be within 1..1024.");
            Format = format; Width = width; Height = height; StorageWidth = GetStorageExtent(width); StorageHeight = GetStorageExtent(height);
            TexelBytes = StorageWidth * StorageHeight * format.BitsPerPixel / 8;
            DescriptorBytes = format.Code == 5 ? StorageWidth * StorageHeight / 8 : 0;
            if (format.Code == 5 && TexelBytes > 128 * 1024) throw new ArgumentException("DS compressed textures must fit one 128 KiB texture slot.");
            if (format.Code <= 7 && TexelBytes > 512 * 1024) throw new ArgumentException("DS textures exceed total 512 KiB texture VRAM.");
        }
        /// <summary>Gets the borrowed format definition.</summary>
        [NativeBorrowedReturn]
        public NintendoDsTextureFormat Format { get; }
        /// <summary>Gets logical width before padding.</summary>
        public int Width { get; }
        /// <summary>Gets logical height before padding.</summary>
        public int Height { get; }
        /// <summary>Gets native storage width.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets native storage height.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets bytes of texels or compressed block selectors.</summary>
        public int TexelBytes { get; }
        /// <summary>Gets bytes of compressed palette-base/mode descriptors.</summary>
        public int DescriptorBytes { get; }
        /// <summary>Rounds a checked extent to a minimum-eight power of two.</summary>
        public static int GetStorageExtent(int value) {
            if (value < 1 || value > 1024) throw new ArgumentOutOfRangeException(nameof(value));
            int result = 8; while (result < value) result *= 2; return result;
        }
        /// <summary>Gets the pixel address for linear textures or row-major pixels within 8-by-8 OBJ/BG tiles.</summary>
        public static int PixelIndex(int x, int y, int width, bool tiled) => tiled ? ((y / 8 * (width / 8) + x / 8) * 64 + y % 8 * 8 + x % 8) : y * width + x;
        /// <summary>Gets the row-major 4-by-4 compression block address.</summary>
        public static int BlockIndex(int x, int y, int width) => y / 4 * (width / 4) + x / 4;
    }
}
