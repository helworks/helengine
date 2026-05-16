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
        A8 = 3
    }
}
