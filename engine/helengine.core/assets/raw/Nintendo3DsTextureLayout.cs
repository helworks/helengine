namespace helengine {
    /// <summary>Calculates single-level PICA200 storage with power-of-two extents and 8-by-8 tiles.</summary>
    public sealed class Nintendo3DsTextureLayout {
        /// <summary>Creates bounded GPU storage while preserving authored dimensions for UV calculations.</summary>
        public Nintendo3DsTextureLayout(Nintendo3DsTextureFormat format, int width, int height) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width < 1 || height < 1 || width > 1024 || height > 1024) throw new ArgumentOutOfRangeException(nameof(width), "3DS textures must fit within 1024 by 1024.");
            Format = format; Width = width; Height = height;
            StorageWidth = PowerOfTwo(width); StorageHeight = PowerOfTwo(height);
            TexelBytes = StorageWidth * StorageHeight * format.BitsPerPixel / 8;
        }
        /// <summary>Gets borrowed hardware encoding metadata.</summary>
        public Nintendo3DsTextureFormat Format { get; }
        /// <summary>Gets the authored width.</summary>
        public int Width { get; }
        /// <summary>Gets the authored height.</summary>
        public int Height { get; }
        /// <summary>Gets the padded GPU width, at least eight pixels.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets the padded GPU height, at least eight pixels.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets the encoded GPU byte count, excluding the asset header.</summary>
        public int TexelBytes { get; }
        /// <summary>Maps an uncompressed texel to row-major tiles with Morton order inside each tile.</summary>
        public static int PixelIndex(int x, int y, int storageWidth) {
            int morton = (x & 1) | (y & 1) << 1 | (x & 2) << 1 | (y & 2) << 2 | (x & 4) << 2 | (y & 4) << 3;
            return (y / 8 * (storageWidth / 8) + x / 8) * 64 + morton;
        }
        /// <summary>Maps a compressed texel to its 4-by-4 block within a row-major 8-by-8 tile.</summary>
        public static int BlockIndex(int x, int y, int storageWidth) => (y / 8 * (storageWidth / 8) + x / 8) * 4 + y % 8 / 4 * 2 + x % 8 / 4;
        /// <summary>Rounds a valid authored extent to a supported GPU extent.</summary>
        static int PowerOfTwo(int value) {
            int result = 8;
            while (result < value) result *= 2;
            return result;
        }
    }
}
