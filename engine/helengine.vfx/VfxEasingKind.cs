namespace helengine.vfx {
    /// <summary>
    /// Easing curves an effect can apply to its normalized clip progress. The numeric values are
    /// written straight into a shader parameter slot, so they must stay stable and must match the
    /// branch order in VfxCommon.hlsli's ApplyEasing.
    /// </summary>
    public enum VfxEasingKind {
        /// <summary>
        /// Constant-rate progress, no acceleration.
        /// </summary>
        Linear = 0,

        /// <summary>
        /// Quadratic ease-in: starts slow and accelerates toward the end of the clip.
        /// </summary>
        EaseIn = 1,

        /// <summary>
        /// Quadratic ease-out: starts fast and decelerates toward the end of the clip.
        /// </summary>
        EaseOut = 2,

        /// <summary>
        /// Quadratic ease-in-out: accelerates through the first half and decelerates through the second.
        /// </summary>
        EaseInOut = 3
    }
}
