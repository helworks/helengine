namespace helengine {
    /// <summary>
    /// Stores all precomputed body, basis, effective-mass, material, penetration, and accumulated-impulse data for one contact.
    /// </summary>
    struct HelPhysicsContactConstraint3D {
        /// <summary>
        /// Stores the fixed pool index of the body from which the contact normal points.
        /// </summary>
        public int BodyAIndex;

        /// <summary>
        /// Stores the fixed pool index of the body toward which the contact normal points.
        /// </summary>
        public int BodyBIndex;

        /// <summary>
        /// Stores the source manifold index used for final impulse writeback.
        /// </summary>
        public int ManifoldIndex;

        /// <summary>
        /// Stores the source inline contact index used for final impulse writeback.
        /// </summary>
        public int ContactIndex;

        /// <summary>
        /// Stores the complete active contact count of the source manifold so writeback can detect layout changes.
        /// </summary>
        public int ManifoldContactCount;

        /// <summary>
        /// Stores the source contact feature identity so same-count reordering cannot receive another contact's impulses.
        /// </summary>
        public HelPhysicsContactFeature3D Feature;

        /// <summary>
        /// Stores the unit world-space contact normal directed from body A toward body B.
        /// </summary>
        public PhysicsVector3 Normal;

        /// <summary>
        /// Stores the first deterministic unit tangent perpendicular to <see cref="Normal"/>.
        /// </summary>
        public PhysicsVector3 Tangent0;

        /// <summary>
        /// Stores the second deterministic unit tangent perpendicular to the normal and first tangent.
        /// </summary>
        public PhysicsVector3 Tangent1;

        /// <summary>
        /// Stores the contact anchor in body A local space so positional passes can rebuild current world geometry.
        /// </summary>
        public PhysicsVector3 LocalAnchorA;

        /// <summary>
        /// Stores the contact anchor in body B local space so positional passes can rebuild current world geometry.
        /// </summary>
        public PhysicsVector3 LocalAnchorB;

        /// <summary>
        /// Stores the world-space contact lever arm from body A's center of mass.
        /// </summary>
        public PhysicsVector3 LeverArmA;

        /// <summary>
        /// Stores the world-space contact lever arm from body B's center of mass.
        /// </summary>
        public PhysicsVector3 LeverArmB;

        /// <summary>
        /// Stores body A's orientation-derived world inverse inertia or zero when it cannot respond.
        /// </summary>
        public PhysicsMatrix3x3 WorldInverseInertiaA;

        /// <summary>
        /// Stores body B's orientation-derived world inverse inertia or zero when it cannot respond.
        /// </summary>
        public PhysicsMatrix3x3 WorldInverseInertiaB;

        /// <summary>
        /// Stores body A's inverse mass or zero when it cannot respond to solver impulses.
        /// </summary>
        public PhysicsScalar InverseMassA;

        /// <summary>
        /// Stores body B's inverse mass or zero when it cannot respond to solver impulses.
        /// </summary>
        public PhysicsScalar InverseMassB;

        /// <summary>
        /// Stores the reciprocal scalar effective mass along the contact normal.
        /// </summary>
        public PhysicsScalar NormalEffectiveMass;

        /// <summary>
        /// Stores the reciprocal scalar effective mass along the first tangent.
        /// </summary>
        public PhysicsScalar TangentEffectiveMass0;

        /// <summary>
        /// Stores the reciprocal scalar effective mass along the second tangent.
        /// </summary>
        public PhysicsScalar TangentEffectiveMass1;

        /// <summary>
        /// Stores the non-negative separating speed requested by restitution for the prepared incoming impact.
        /// </summary>
        public PhysicsScalar RestitutionVelocity;

        /// <summary>
        /// Stores the geometric mean of both bodies' static friction coefficients.
        /// </summary>
        public PhysicsScalar StaticFriction;

        /// <summary>
        /// Stores the geometric mean of both bodies' dynamic friction coefficients.
        /// </summary>
        public PhysicsScalar DynamicFriction;

        /// <summary>
        /// Stores current non-negative overlap depth for split positional correction.
        /// </summary>
        public PhysicsScalar PenetrationDepth;

        /// <summary>
        /// Stores the normal impulse accumulated through warm starting and velocity iterations.
        /// </summary>
        public PhysicsScalar AccumulatedNormalImpulse;

        /// <summary>
        /// Stores the first tangent impulse accumulated through warm starting and velocity iterations.
        /// </summary>
        public PhysicsScalar AccumulatedTangentImpulse0;

        /// <summary>
        /// Stores the second tangent impulse accumulated through warm starting and velocity iterations.
        /// </summary>
        public PhysicsScalar AccumulatedTangentImpulse1;

        /// <summary>
        /// Indicates whether body A is an awake dynamic participant that receives impulses.
        /// </summary>
        public bool RespondsA;

        /// <summary>
        /// Indicates whether body B is an awake dynamic participant that receives impulses.
        /// </summary>
        public bool RespondsB;
    }
}
