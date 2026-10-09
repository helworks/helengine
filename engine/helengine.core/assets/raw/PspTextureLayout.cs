namespace helengine {
    /// <summary>Calculates bounded power-of-two PSP storage while retaining authored UV dimensions.</summary>
    public sealed class PspTextureLayout {
        /// <summary>Creates canonical single-level storage within the GE's 512 by 512 limit.</summary>
        public PspTextureLayout(PspTextureFormat format, int width, int height) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width < 1 || height < 1 || width > 512 || height > 512) throw new ArgumentOutOfRangeException(nameof(width), "PSP textures must fit within 512 by 512.");
            Format = format;
            Width = width;
            Height = height;
            StorageWidth = PowerOfTwo(width, format.IsCompressed ? 4 : 128 / format.BitsPerPixel);
            StorageHeight = PowerOfTwo(height, format.IsCompressed ? 4 : format.Swizzled ? 8 : 1);
            Pitch = StorageWidth * format.BitsPerPixel / 8;
            TexelBytes = Pitch * StorageHeight;
            PaletteEntries = format.IsIndexed ? format.Code == 4 ? 16 : 256 : 0;
            PaletteBytes = PaletteEntries * (format.PaletteCode == 3 ? 4 : 2);
        }
        /// <summary>Gets the native hardware format and layout.</summary>
        public PspTextureFormat Format { get; }
        /// <summary>Gets the authored width.</summary>
        public int Width { get; }
        /// <summary>Gets the authored height.</summary>
        public int Height { get; }
        /// <summary>Gets the GU power-of-two width and texel buffer width.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets the GU power-of-two height.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets the uncompressed byte pitch or average DXT row size.</summary>
        public int Pitch { get; }
        /// <summary>Gets the entire GPU texel allocation size.</summary>
        public int TexelBytes { get; }
        /// <summary>Gets the number of CLUT entries; T16/T32 use the canonical low-eight-bit index mask.</summary>
        public int PaletteEntries { get; }
        /// <summary>Gets native palette storage, including the complete CLUT load budget.</summary>
        public int PaletteBytes { get; }
        /// <summary>Maps an uncompressed logical byte to its linear or 16-byte by 8-row location.</summary>
        public int ByteOffset(int xByte, int y) => Format.Swizzled
            ? (y / 8 * (Pitch / 16) + xByte / 16) * 128 + y % 8 * 16 + xByte % 16 : y * Pitch + xByte;
        /// <summary>Rounds an authored extent up to a representable GU image extent.</summary>
        static int PowerOfTwo(int value, int minimum) {
            int result = minimum;
            while (result < value) result *= 2;
            return result;
        }
    }
}
