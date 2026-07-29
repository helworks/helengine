namespace helengine {
    /// <summary>
    /// Identifies which ordered family of oriented-box separating axes produced a minimum-penetration result.
    /// </summary>
    enum HelPhysicsBoxSatAxisKind3D {
        /// <summary>
        /// Indicates that one of box A's local face normals produced the minimum penetration.
        /// </summary>
        FaceA,

        /// <summary>
        /// Indicates that one of box B's local face normals produced the minimum penetration.
        /// </summary>
        FaceB,

        /// <summary>
        /// Indicates that the normalized cross product of one edge direction from each box produced the minimum penetration.
        /// </summary>
        EdgePair
    }
}
