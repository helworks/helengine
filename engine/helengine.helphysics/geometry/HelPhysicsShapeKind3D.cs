namespace helengine {
    /// <summary>
    /// Identifies the primitive geometry stored by a HelPhysics shape slot.
    /// </summary>
    public enum HelPhysicsShapeKind3D : byte {
        /// <summary>
        /// A centered oriented box described by three positive half extents.
        /// </summary>
        Box = 0,

        /// <summary>
        /// A sphere described by one positive radius.
        /// </summary>
        Sphere = 1
    }
}
