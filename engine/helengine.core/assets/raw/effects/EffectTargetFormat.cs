namespace helengine {
    /// <summary>
    /// Selects the storage format of an effect's intermediate render target.
    /// </summary>
    public enum EffectTargetFormat {
        /// <summary>
        /// Four 16-bit float channels; keeps linear HDR values and soft alpha gradients precise.
        /// </summary>
        Rgba16Float = 0,

        /// <summary>
        /// Four 8-bit normalized channels; half the bandwidth for masks and other low-precision data.
        /// </summary>
        Rgba8 = 1
    }
}
