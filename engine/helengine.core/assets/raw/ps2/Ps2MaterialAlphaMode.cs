namespace helengine {
    /// <summary>
    /// Identifies the alpha behavior selected by one cooked PS2 material payload.
    /// </summary>
    public enum Ps2MaterialAlphaMode {
        /// <summary>
        /// Draws the material as fully opaque.
        /// </summary>
        Opaque,

        /// <summary>
        /// Draws the material using an alpha-test cutoff.
        /// </summary>
        AlphaTest,

        /// <summary>
        /// Draws the material using alpha blending.
        /// </summary>
        AlphaBlend,

        /// <summary>
        /// Draws the material using additive blending.
        /// </summary>
        Additive
    }
}
