namespace helengine {
    /// <summary>
    /// Captures one immutable copy of body identity-independent simulation and lifecycle state at an API boundary.
    /// </summary>
    public readonly struct HelPhysicsBodySnapshot3D {
        /// <summary>
        /// Stores the body's simulation participation mode.
        /// </summary>
        public readonly BodyKind3D BodyKind;

        /// <summary>
        /// Stores the copied world-space center-of-mass position.
        /// </summary>
        public readonly PhysicsVector3 Position;

        /// <summary>
        /// Stores the copied world-space orientation.
        /// </summary>
        public readonly PhysicsQuaternion Orientation;

        /// <summary>
        /// Stores the copied world-space linear velocity.
        /// </summary>
        public readonly PhysicsVector3 LinearVelocity;

        /// <summary>
        /// Stores the copied world-space angular velocity.
        /// </summary>
        public readonly PhysicsVector3 AngularVelocity;

        /// <summary>
        /// Stores the synchronized low-motion step count used by island sleeping.
        /// </summary>
        public readonly ushort LowMotionStepCount;

        /// <summary>
        /// Indicates whether the body currently belongs to the awake dynamic simulation set.
        /// </summary>
        public readonly bool IsAwake;

        /// <summary>
        /// Indicates whether deferred creation has published this reserved body to broadphase and solving.
        /// </summary>
        public readonly bool IsActive;

        /// <summary>
        /// Indicates whether the reserved body is waiting for its deferred creation command to execute.
        /// </summary>
        public readonly bool IsPending;

        /// <summary>
        /// Initializes one immutable snapshot from copied hot, cold, and world lifecycle state.
        /// </summary>
        /// <param name="bodyKind">Simulation participation mode.</param>
        /// <param name="position">World-space center-of-mass position.</param>
        /// <param name="orientation">World-space orientation.</param>
        /// <param name="linearVelocity">World-space linear velocity.</param>
        /// <param name="angularVelocity">World-space angular velocity.</param>
        /// <param name="lowMotionStepCount">Current synchronized quiet duration.</param>
        /// <param name="isAwake">Current dynamic awake state.</param>
        /// <param name="isActive">Whether creation has entered active simulation storage.</param>
        public HelPhysicsBodySnapshot3D(
            BodyKind3D bodyKind,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity,
            ushort lowMotionStepCount,
            bool isAwake,
            bool isActive) {
            BodyKind = bodyKind;
            Position = position;
            Orientation = orientation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            LowMotionStepCount = lowMotionStepCount;
            IsAwake = isAwake;
            IsActive = isActive;
            IsPending = !isActive;
        }
    }
}
