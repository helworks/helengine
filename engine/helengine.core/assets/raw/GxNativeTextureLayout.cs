namespace helengine {
    /// <summary>Records logical GX dimensions and complete native storage tiles without changing normalized coordinates.</summary>
    public sealed class GxNativeTextureLayout {
        /// <summary>Builds a validated single-level layout using a borrowed catalog description.</summary>
        public GxNativeTextureLayout(GxNativeTextureFormatDefinition format, int width, int height, int paletteEntryCount) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            TexelLength = format.GetPixelByteLength(width, height); format.GetVramByteLength(width, height, paletteEntryCount);
            Format = format; RealWidth = width; RealHeight = height;
            StorageWidth = (width + format.BlockWidth - 1) / format.BlockWidth * format.BlockWidth;
            StorageHeight = (height + format.BlockHeight - 1) / format.BlockHeight * format.BlockHeight;
            PaletteEntryCount = paletteEntryCount; PaletteLength = paletteEntryCount * 2;
        }
        /// <summary>Gets the borrowed native texture and palette description.</summary>
        [NativeBorrowedReturn] public GxNativeTextureFormatDefinition Format { get; }
        /// <summary>Gets the logical width passed to GX_InitTexObj.</summary>
        public int RealWidth { get; }
        /// <summary>Gets the logical height passed to GX_InitTexObj.</summary>
        public int RealHeight { get; }
        /// <summary>Gets width rounded to complete native tiles.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets height rounded to complete native tiles.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets native texel bytes excluding the descriptor.</summary>
        public int TexelLength { get; }
        /// <summary>Gets actual TLUT entries including sixteen-entry padding.</summary>
        public int PaletteEntryCount { get; }
        /// <summary>Gets separate big-endian TLUT bytes.</summary>
        public int PaletteLength { get; }
    }
}
