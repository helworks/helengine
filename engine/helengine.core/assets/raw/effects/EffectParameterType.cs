namespace helengine {
    /// <summary>
    /// Describes the value shape of an effect parameter and how many constant slots it occupies.
    /// </summary>
    public enum EffectParameterType {
        /// <summary>
        /// One number in one slot.
        /// </summary>
        Float = 0,

        /// <summary>
        /// One whole number in one slot.
        /// </summary>
        Integer = 1,

        /// <summary>
        /// Two numbers in two consecutive slots.
        /// </summary>
        Float2 = 2,

        /// <summary>
        /// Four numbers in four consecutive slots.
        /// </summary>
        Float4 = 3,

        /// <summary>
        /// An RGBA color in four consecutive slots; RGB-only values receive an alpha of one.
        /// </summary>
        Color = 4,

        /// <summary>
        /// A switch written as zero or one in one slot.
        /// </summary>
        Bool = 5,

        /// <summary>
        /// One of <see cref="EffectParameterAsset.AllowedValues"/>, written as its index in one slot.
        /// </summary>
        Enum = 6
    }
}
