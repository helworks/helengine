namespace helengine {
    /// <summary>
    /// Identifies how one <see cref="TextureAsset"/> stores its serialized color payload.
    /// </summary>
    public enum TextureAssetColorFormat : byte {
        /// <summary>
        /// Stores one texel as four 8-bit RGBA channels.
        /// </summary>
        Rgba32 = 0,

        /// <summary>
        /// Stores one texel as four 4-bit RGBA channels packed into a 16-bit word.
        /// </summary>
        Rgba4444 = 1,

        /// <summary>
        /// Stores one texel as one 4-bit palette index.
        /// </summary>
        Indexed4 = 2,

        /// <summary>
        /// Stores one texel as one 8-bit palette index.
        /// </summary>
        Indexed8 = 3,

        /// <summary>
        /// Stores one texel as one GX RGB5A3 word laid out in native 4x4 tiled order.
        /// </summary>
        GxRgb5A3 = 4
    }
}
