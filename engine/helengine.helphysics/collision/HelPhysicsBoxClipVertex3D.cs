namespace helengine {
    /// <summary>
    /// Carries one face-clipping position together with incident vertices and reference planes that created it.
    /// </summary>
    struct HelPhysicsBoxClipVertex3D {
        /// <summary>
        /// Stores the current world-space polygon vertex position.
        /// </summary>
        public PhysicsVector3 Position;

        /// <summary>
        /// Stores a bit for every original incident-box vertex contributing to this clipped point.
        /// </summary>
        public byte IncidentVertexMask;

        /// <summary>
        /// Stores a bit for every reference side plane that clipped this point.
        /// </summary>
        public byte ClipPlaneMask;

        /// <summary>
        /// Initializes one clipping vertex and its deterministic geometric provenance.
        /// </summary>
        /// <param name="position">World-space polygon position.</param>
        /// <param name="incidentVertexMask">Bits identifying contributing original incident vertices.</param>
        /// <param name="clipPlaneMask">Bits identifying reference side planes that created the point.</param>
        public HelPhysicsBoxClipVertex3D(PhysicsVector3 position, byte incidentVertexMask, byte clipPlaneMask) {
            Position = position;
            IncidentVertexMask = incidentVertexMask;
            ClipPlaneMask = clipPlaneMask;
        }
    }
}
