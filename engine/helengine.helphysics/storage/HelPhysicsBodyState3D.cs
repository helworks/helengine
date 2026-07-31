namespace helengine {
    /// <summary>
    /// Stores body data read and updated by hot integration and solver loops.
    /// </summary>
    struct HelPhysicsBodyState3D {
        /// <summary>
        /// Stores the body's world-space center-of-mass position.
        /// </summary>
        public PhysicsVector3 Position;

        /// <summary>
        /// Stores the body's world-space orientation.
        /// </summary>
        public PhysicsQuaternion Orientation;

        /// <summary>
        /// Stores the body's world-space linear velocity.
        /// </summary>
        public PhysicsVector3 LinearVelocity;

        /// <summary>
        /// Stores the body's world-space angular velocity.
        /// </summary>
        public PhysicsVector3 AngularVelocity;

        /// <summary>
        /// Stores world-space force accumulated since this body last integrated velocity.
        /// </summary>
        public PhysicsVector3 AccumulatedForce;

        /// <summary>
        /// Stores world-space torque accumulated since this body last integrated velocity.
        /// </summary>
        public PhysicsVector3 AccumulatedTorque;

        /// <summary>
        /// Stores reciprocal body mass for efficient solver mass calculations.
        /// </summary>
        public PhysicsScalar InverseMass;

        /// <summary>
        /// Stores the inverse inertia tensor in the body's local frame.
        /// </summary>
        public PhysicsMatrix3x3 LocalInverseInertia;

        /// <summary>
        /// Stores the multiplier applied to world gravity for this body.
        /// </summary>
        public PhysicsScalar GravityScale;

        /// <summary>
        /// Stores the damping factor applied to linear velocity.
        /// </summary>
        public PhysicsScalar LinearDamping;

        /// <summary>
        /// Stores the damping factor applied to angular velocity.
        /// </summary>
        public PhysicsScalar AngularDamping;

        /// <summary>
        /// Stores consecutive low-motion simulation steps used by later sleep decisions.
        /// </summary>
        public ushort LowMotionStepCount;

        /// <summary>
        /// Indicates whether integration and solving should currently process this body.
        /// </summary>
        public bool IsAwake;

        /// <summary>
        /// Indicates whether this fixed storage slot currently owns one allocated body.
        /// </summary>
        public bool IsOccupied;
    }
}
