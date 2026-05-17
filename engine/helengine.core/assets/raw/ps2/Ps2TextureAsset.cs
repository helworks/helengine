namespace helengine {
    /// <summary>
    /// Stores one PS2-native cooked runtime texture payload produced by the PS2 builder.
    /// </summary>
    public class Ps2TextureAsset : Asset {
        /// <summary>
        /// Gets or sets the runtime texture width in pixels.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// Gets or sets the runtime texture height in pixels.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// Gets or sets the PS2-native texture payload format.
        /// </summary>
        public Ps2TextureFormat Format;

        /// <summary>
        /// Gets or sets the PS2-native alpha storage behavior.
        /// </summary>
        public Ps2TextureAlphaMode AlphaMode;

        /// <summary>
        /// Gets or sets the packed PS2 texture payload bytes.
        /// </summary>
        public byte[] PixelData;

        /// <summary>
        /// Gets or sets the optional palette payload for PS2-native indexed texture formats.
        /// </summary>
        public byte[] PaletteData;
    }
}
