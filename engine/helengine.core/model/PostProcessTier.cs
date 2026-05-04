namespace helengine {
    /// <summary>
    /// Describes the authored post-processing intensity tier for one camera.
    /// </summary>
    public enum PostProcessTier : byte {
        /// <summary>
        /// Disables post-processing for the camera.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Requests a lightweight post-processing chain.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Requests the standard full post-processing chain.
        /// </summary>
        High = 2
    }
}
