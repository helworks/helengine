namespace helengine {
    /// <summary>
    /// Represents raw texture data stored in memory.
    /// </summary>
    public class TextureAsset : Asset {
        /// <summary>
        /// Raw color data for the texture in RGBA order.
        /// </summary>
        public byte[] Colors;

        /// <summary>
        /// Optional palette payload used by indexed cooked texture formats.
        /// </summary>
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
    }
}
