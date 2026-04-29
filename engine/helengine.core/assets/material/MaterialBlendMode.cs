namespace helengine {
    /// <summary>
    /// Describes how material pixel output should blend with the existing render target contents.
    /// </summary>
    public enum MaterialBlendMode {
        /// <summary>
        /// Writes color without alpha blending.
        /// </summary>
        Opaque,

        /// <summary>
        /// Blends color using standard source-alpha transparency.
        /// </summary>
        AlphaBlend
    }
}
