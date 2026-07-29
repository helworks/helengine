namespace helengine {
    /// <summary>
    /// Owns reusable fixed-capacity clipping buffers required by allocation-free box manifold queries.
    /// </summary>
    sealed class HelPhysicsBoxCollisionScratch3D {
        /// <summary>
        /// Stores the first alternating polygon buffer; a clipped quadrilateral can grow to at most eight vertices.
        /// </summary>
        public readonly HelPhysicsBoxClipVertex3D[] ClippingBuffer0;

        /// <summary>
        /// Stores the second alternating polygon buffer used as each clipping plane writes its output.
        /// </summary>
        public readonly HelPhysicsBoxClipVertex3D[] ClippingBuffer1;

        /// <summary>
        /// Allocates both maximum-size clipping buffers once for reuse by every query owned by this scratch instance.
        /// </summary>
        public HelPhysicsBoxCollisionScratch3D() {
            ClippingBuffer0 = new HelPhysicsBoxClipVertex3D[8];
            ClippingBuffer1 = new HelPhysicsBoxClipVertex3D[8];
        }
    }
}
