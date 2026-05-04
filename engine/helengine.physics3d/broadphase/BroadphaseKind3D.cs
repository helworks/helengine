namespace helengine {
    /// <summary>
    /// Enumerates the broadphase strategies available to one 3D physics world.
    /// </summary>
    public enum BroadphaseKind3D {
        /// <summary>
        /// Uses a fixed spatial grid to bucket dynamic bodies into uniform cells.
        /// </summary>
        UniformGrid,

        /// <summary>
        /// Uses sorted axis intervals to generate dynamic-body candidate pairs.
        /// </summary>
        SweepAndPrune
    }
}
