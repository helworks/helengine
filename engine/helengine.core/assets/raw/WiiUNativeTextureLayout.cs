namespace helengine {
    /// <summary>Computes compact GX2 LINEAR_SPECIAL transport independently of the runtime's SDK-aligned GPU allocation.</summary>
    public sealed class WiiUNativeTextureLayout {
        /// <summary>Constructs exact single-level block rows or even-sized NV12 planes for logical two-dimensional sampling.</summary>
        public WiiUNativeTextureLayout(WiiUNativeTextureFormatDefinition format, int width, int height) {
            if (format == null) throw new ArgumentNullException(nameof(format)); if (width < 1 || height < 1 || width > 8192 || height > 8192) throw new ArgumentOutOfRangeException("GX2 dimensions must be within 1..8192.");
            Format = format; RealWidth = width; RealHeight = height; StorageWidth = Align(width, format.IsNv12 ? 2 : format.BlockWidth); StorageHeight = Align(height, format.IsNv12 ? 2 : format.BlockHeight);
            PitchBytes = format.IsNv12 ? StorageWidth : format.IsBlockCompressed ? StorageWidth / 4 * format.BytesPerBlock : StorageWidth * format.BitsPerPixel / 8;
            UvPlaneOffset = format.IsNv12 ? checked(StorageWidth * StorageHeight) : 0;
            TexelLength = format.IsNv12 ? checked(UvPlaneOffset + UvPlaneOffset / 2) : checked(PitchBytes * (StorageHeight / format.BlockHeight));
        }
        /// <summary>Gets the borrowed catalog format and canonical sampler controls.</summary>
        [NativeBorrowedReturn] public WiiUNativeTextureFormatDefinition Format { get; }
        /// <summary>Gets real horizontal sampling extent.</summary>
        public int RealWidth { get; }
        /// <summary>Gets real vertical sampling extent.</summary>
        public int RealHeight { get; }
        /// <summary>Gets compact allocation width in texels, including complete blocks or chroma pairs.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets compact allocation height, including complete blocks or chroma rows.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets native scanline or compressed block-row bytes.</summary>
        public int PitchBytes { get; }
        /// <summary>Gets exact texel and plane payload bytes.</summary>
        public int TexelLength { get; }
        /// <summary>Gets compact NV12 UV-plane byte offset, zero for other formats.</summary>
        public int UvPlaneOffset { get; }
        /// <summary>Gets the SDK transport mode; runtime sampling uses a separately calculated aligned surface.</summary>
        public uint TileMode { get { return 16; } }
        /// <summary>Gets fixed native texel endian mode, independent of outer asset metadata.</summary>
        public uint EndianSwap { get { return 0; } }
        /// <summary>Rounds storage extents to complete native blocks or chroma pairs.</summary>
        static int Align(int value, int alignment) { return (value + alignment - 1) / alignment * alignment; }
    }
}
