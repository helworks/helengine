namespace helengine {
    /// <summary>
    /// Represents raw texture data stored in memory.
    /// </summary>
    public class TextureAsset : Asset, IDisposable {
        /// <summary>
        /// Tracks whether this raw texture has already released its native pixel buffers.
        /// </summary>
        bool IsDisposedValue;

        /// <summary>
        /// Serialized texels in the layout selected by <see cref="ColorFormat"/>.
        /// RGBA32 uses RGBA bytes. Legacy RGBA4444 uses little-endian words with red in the low nibble;
        /// legacy Indexed4 packs its first pixel in the low nibble across the whole image.
        /// New 16-bit RGB/IA formats use little-endian words, new 4-bit I/IA formats pack high-nibble first
        /// with byte-aligned rows, and YUV16 uses Y0/U/Y1/V pairs within each row.
        /// <see cref="TextureAssetPixelCodec"/> validates and converts these layouts without mutating this buffer.
        /// </summary>
        [NativeOwnedMember]
        public byte[] Colors;

        /// <summary>
        /// Optional palette payload used by indexed cooked texture formats.
        /// Switch native textures use this auxiliary buffer for their decoded RGBA CPU preview; it is not GPU palette data.
        /// </summary>
        [NativeOwnedMember]
        public byte[] PaletteColors;

        /// <summary>
        /// Width of the texture in pixels.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// Height of the texture in pixels.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// Describes how the serialized texture payload stores its pixel data.
        /// </summary>
        public TextureAssetColorFormat ColorFormat;

        /// <summary>
        /// Describes the alpha precision stored by the serialized texture payload.
        /// </summary>
        public TextureAssetAlphaPrecision AlphaPrecision;

        /// <summary>
        /// Indicates whether this raw texture payload is created by engine infrastructure instead of scene-authored content.
        /// </summary>
        public bool IsEngineOwned;

        /// <summary>
        /// Releases the pixel and palette buffers owned by this raw texture asset.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Colors);
            NativeOwnership.Release(ref PaletteColors);
            IsDisposedValue = true;
        }
    }
}
