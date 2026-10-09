namespace helengine {
    /// <summary>Computes canonical single-level linear Xenos block rows without changing authored bounds.</summary>
    public sealed class Xbox360NativeTextureLayout {
        /// <summary>Constructs pitch aligned to thirty-two blocks and 256 bytes, with complete final block rows.</summary>
        public Xbox360NativeTextureLayout(Xbox360NativeTextureFormatDefinition format, int width, int height) {
            if (format == null || !format.SupportsCooking) throw new ArgumentException("Xenos format has no verified texture codec.");
            if (width < 1 || height < 1 || width > 8192 || height > 8192) throw new ArgumentOutOfRangeException("Texture dimensions must be within 1..8192.");
            Format = format; RealWidth = width; RealHeight = height;
            int blocks = ((width + format.BlockWidth - 1) / format.BlockWidth + 31) / 32 * 32;
            while ((blocks * format.BytesPerBlock) % 256 != 0) blocks += 32;
            StorageWidth = checked(blocks * format.BlockWidth);
            if (StorageWidth > 8192) throw new ArgumentException("Canonical texture pitch exceeds 8192 texels.");
            StorageHeight = (height + format.BlockHeight - 1) / format.BlockHeight * format.BlockHeight;
            PitchBytes = checked(blocks * format.BytesPerBlock);
            TexelLength = checked(PitchBytes * (StorageHeight / format.BlockHeight));
        }
        /// <summary>Gets the borrowed format description.</summary>
        [NativeBorrowedReturn] public Xbox360NativeTextureFormatDefinition Format { get; }
        /// <summary>Gets authored width used by the texture fetch size.</summary>
        public int RealWidth { get; }
        /// <summary>Gets authored height used by the texture fetch size.</summary>
        public int RealHeight { get; }
        /// <summary>Gets the canonical pitch expressed in texels.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets the height rounded to complete storage blocks.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets bytes between successive block rows.</summary>
        public int PitchBytes { get; }
        /// <summary>Gets the complete GPU payload size including row padding.</summary>
        public int TexelLength { get; }
    }
}
