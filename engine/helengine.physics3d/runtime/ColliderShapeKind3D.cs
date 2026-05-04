namespace helengine {
    /// <summary>
    /// Identifies the concrete collider shape bound to one runtime body state.
    /// </summary>
    public enum ColliderShapeKind3D : byte {
        /// <summary>
        /// Axis-aligned box collider shape.
        /// </summary>
        Box = 0,

        /// <summary>
        /// Sphere collider shape.
        /// </summary>
        Sphere = 1,

        /// <summary>
        /// Vertically aligned capsule collider shape.
        /// </summary>
        Capsule = 2
    }
}
