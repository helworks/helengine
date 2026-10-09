namespace helengine {
    /// <summary>
    /// Identifies the alpha precision stored by one cooked texture payload.
    /// </summary>
    public enum TextureAssetAlphaPrecision : byte {
        /// <summary>
        /// Stores no alpha data and treats all texels as opaque.
        /// </summary>
        Opaque = 0,

        /// <summary>
        /// Stores thresholded transparent or opaque alpha values.
        /// </summary>
        Binary = 1,

        /// <summary>
        /// Stores alpha values quantized to 4-bit precision.
        /// </summary>
        A4 = 2,

        /// <summary>
        /// Stores alpha values at 8-bit precision.
        /// </summary>
        A8 = 3,

        /// <summary>Stores four evenly spaced two-bit alpha levels; supported by native Xbox 360 RGB10A2 textures.</summary>
        A2 = 4,

        /// <summary>Stores three-bit alpha in native GX RGB5A3 colors or Nintendo DS A3I5 texels.</summary>
        A3 = 5,

        /// <summary>Stores thirty-two alpha levels in native Nintendo DS A5I3 texels.</summary>
        A5 = 6
    }
}
