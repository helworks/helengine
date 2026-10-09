namespace helengine.editor {
    /// <summary>Stores checked base-level image metadata borrowed from one bounded GIM container.</summary>
    internal sealed class PspGimImageInfo {
        /// <summary>Creates metadata after checking its block and pixel bounds.</summary>
        public PspGimImageInfo(int code, int width, int height, int pitch, int storageHeight, bool swizzled, int pixelOffset) {
            Code = code; Width = width; Height = height; Pitch = pitch; StorageHeight = storageHeight; Swizzled = swizzled; PixelOffset = pixelOffset;
        }
        /// <summary>Gets the GU pixel format.</summary>
        public int Code { get; }
        /// <summary>Gets the authored image width.</summary>
        public int Width { get; }
        /// <summary>Gets the authored image height.</summary>
        public int Height { get; }
        /// <summary>Gets the row or compressed block-row byte pitch.</summary>
        public int Pitch { get; }
        /// <summary>Gets the aligned image height.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets whether uncompressed bytes use PSP faster order.</summary>
        public bool Swizzled { get; }
        /// <summary>Gets the absolute first-frame base-level pixel offset.</summary>
        public int PixelOffset { get; }
    }
}
