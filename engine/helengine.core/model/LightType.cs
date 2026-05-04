namespace helengine {
    /// <summary>
    /// Identifies the authored light family used by shared render planning and backend execution.
    /// </summary>
    public enum LightType : byte {
        /// <summary>
        /// Directional light with no range falloff.
        /// </summary>
        Directional = 0,

        /// <summary>
        /// Omnidirectional point light with range falloff.
        /// </summary>
        Point = 1,

        /// <summary>
        /// Cone-shaped spot light with range falloff.
        /// </summary>
        Spot = 2
    }
}
