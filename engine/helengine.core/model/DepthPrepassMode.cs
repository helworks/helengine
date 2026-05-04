namespace helengine {
    /// <summary>
    /// Describes how one camera prefers to use a depth prepass before forward shading.
    /// </summary>
    public enum DepthPrepassMode : byte {
        /// <summary>
        /// Lets the active backend decide whether a depth prepass is worthwhile.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Disables the depth prepass for the authored camera.
        /// </summary>
        Disabled = 1,

        /// <summary>
        /// Requires the active backend to schedule a depth prepass whenever it supports one.
        /// </summary>
        Always = 2
    }
}
