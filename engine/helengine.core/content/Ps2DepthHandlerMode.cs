namespace helengine {
    /// <summary>
    /// Describes how the PS2 runtime should handle depth buffering for forward rendering.
    /// </summary>
    public enum Ps2DepthHandlerMode : byte {
        /// <summary>
        /// Uses the GS hardware depth buffer for opaque and depth-tested rendering.
        /// </summary>
        Hardware = 0,

        /// <summary>
        /// Keeps the software-style depth path active without enabling GS depth writes.
        /// </summary>
        Software = 1
    }
}
