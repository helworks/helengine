namespace helengine {
    /// <summary>
    /// Identifies the alpha storage behavior selected for one PS2-native runtime texture payload.
    /// </summary>
    public enum Ps2TextureAlphaMode : byte {
        /// <summary>
        /// Treats the texture as fully opaque.
        /// </summary>
        Opaque = 0,

        /// <summary>
        /// Stores full alpha values in the PS2-native payload.
        /// </summary>
        Full = 1
    }
}
