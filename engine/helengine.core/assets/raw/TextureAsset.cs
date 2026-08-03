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
        /// Raw color data for the texture in RGBA order.
        /// </summary>
        [NativeOwnedMember]
        public byte[] Colors;

        /// <summary>
        /// Optional palette payload used by indexed cooked texture formats.
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
