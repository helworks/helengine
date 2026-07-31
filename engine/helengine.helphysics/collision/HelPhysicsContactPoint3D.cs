namespace helengine {
    /// <summary>
    /// Stores contact geometry, body-local persistence anchors, and solver impulses for one manifold point.
    /// </summary>
    struct HelPhysicsContactPoint3D {
        /// <summary>
        /// Stores the world-space midpoint between the two penetrating surface anchors.
        /// </summary>
        public PhysicsVector3 Position;

        /// <summary>
        /// Stores the unit world-space contact normal directed from manifold body A toward body B.
        /// </summary>
        public PhysicsVector3 Normal;

        /// <summary>
        /// Stores the contact anchor on body A relative to its center and expressed in its local orientation.
        /// </summary>
        public PhysicsVector3 LocalAnchorA;

        /// <summary>
        /// Stores the contact anchor on body B relative to its center and expressed in its local orientation.
        /// </summary>
        public PhysicsVector3 LocalAnchorB;

        /// <summary>
        /// Stores the non-negative overlap distance associated with this contact point.
        /// </summary>
        public PhysicsScalar PenetrationDepth;

        /// <summary>
        /// Stores deterministic face-clipping or support-edge provenance for persistent matching.
        /// </summary>
        public HelPhysicsContactFeature3D Feature;

        /// <summary>
        /// Stores the normal impulse accumulated by earlier solver iterations or a matched previous manifold.
        /// </summary>
        public PhysicsScalar AccumulatedNormalImpulse;

        /// <summary>
        /// Stores the accumulated impulse along the solver's first contact tangent.
        /// </summary>
        public PhysicsScalar AccumulatedTangentImpulse0;

        /// <summary>
        /// Stores the accumulated impulse along the solver's second contact tangent.
        /// </summary>
        public PhysicsScalar AccumulatedTangentImpulse1;

        /// <summary>
        /// Stores how many prior simulation steps retained this contact before the current narrow-phase result.
        /// </summary>
        public int PreviousStepLifetime;

        /// <summary>
        /// Initializes one newly generated contact and explicitly clears all solver and previous-step state.
        /// </summary>
        /// <param name="position">World-space midpoint between the two surface anchors.</param>
        /// <param name="normal">Unit world-space normal directed from body A toward body B.</param>
        /// <param name="localAnchorA">Surface anchor expressed in body A's local frame.</param>
        /// <param name="localAnchorB">Surface anchor expressed in body B's local frame.</param>
        /// <param name="penetrationDepth">Non-negative overlap distance at this point.</param>
        /// <param name="feature">Deterministic geometric provenance identifier.</param>
        public HelPhysicsContactPoint3D(
            PhysicsVector3 position,
            PhysicsVector3 normal,
            PhysicsVector3 localAnchorA,
            PhysicsVector3 localAnchorB,
            PhysicsScalar penetrationDepth,
            HelPhysicsContactFeature3D feature) {
            Position = position;
            Normal = normal;
            LocalAnchorA = localAnchorA;
            LocalAnchorB = localAnchorB;
            PenetrationDepth = penetrationDepth;
            Feature = feature;
            AccumulatedNormalImpulse = PhysicsScalar.Zero;
            AccumulatedTangentImpulse0 = PhysicsScalar.Zero;
            AccumulatedTangentImpulse1 = PhysicsScalar.Zero;
            PreviousStepLifetime = 0;
        }
    }
}
