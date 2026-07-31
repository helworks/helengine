namespace helengine {
    /// <summary>
    /// Stores one allocation-free deferred general world mutation with a generation-safe body identity and optional state input.
    /// </summary>
    readonly struct HelPhysicsDeferredCommand3D {
        /// <summary>
        /// Stores the exact mutation executed when the command reaches the next fixed-step boundary.
        /// </summary>
        public readonly HelPhysicsDeferredCommandKind3D Kind;

        /// <summary>
        /// Stores the pool-internal body identity captured when the command was accepted.
        /// </summary>
        public readonly HelPhysicsBodyHandle3D BodyHandle;

        /// <summary>
        /// Stores a world-space force or impulse and remains zero for activation and kinematic-state commands.
        /// </summary>
        public readonly PhysicsVector3 Vector;

        /// <summary>
        /// Stores a complete world-space position for a deferred kinematic state replacement.
        /// </summary>
        public readonly PhysicsVector3 Position;

        /// <summary>
        /// Stores a complete world-space orientation for a deferred kinematic state replacement.
        /// </summary>
        public readonly PhysicsQuaternion Orientation;

        /// <summary>
        /// Stores a complete world-space linear velocity for a deferred kinematic state replacement.
        /// </summary>
        public readonly PhysicsVector3 LinearVelocity;

        /// <summary>
        /// Stores a complete world-space angular velocity for a deferred kinematic state replacement.
        /// </summary>
        public readonly PhysicsVector3 AngularVelocity;

        /// <summary>
        /// Initializes one complete command value for deterministic insertion-order execution.
        /// </summary>
        /// <param name="kind">Mutation represented by this command.</param>
        /// <param name="bodyHandle">Current pool-internal body identity targeted by the mutation.</param>
        /// <param name="vector">World-space force or impulse, or zero for activation.</param>
        public HelPhysicsDeferredCommand3D(
            HelPhysicsDeferredCommandKind3D kind,
            HelPhysicsBodyHandle3D bodyHandle,
            PhysicsVector3 vector) {
            Kind = kind;
            BodyHandle = bodyHandle;
            Vector = vector;
            Position = PhysicsVector3.Zero;
            Orientation = PhysicsQuaternion.Identity;
            LinearVelocity = PhysicsVector3.Zero;
            AngularVelocity = PhysicsVector3.Zero;
        }

        /// <summary>
        /// Initializes one complete deferred kinematic state replacement.
        /// </summary>
        /// <param name="bodyHandle">Current pool-internal kinematic body identity.</param>
        /// <param name="position">Validated world-space body position.</param>
        /// <param name="orientation">Validated normalized world-space body orientation.</param>
        /// <param name="linearVelocity">Validated world-space linear velocity.</param>
        /// <param name="angularVelocity">Validated world-space angular velocity.</param>
        public HelPhysicsDeferredCommand3D(
            HelPhysicsBodyHandle3D bodyHandle,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            Kind = HelPhysicsDeferredCommandKind3D.SetKinematicState;
            BodyHandle = bodyHandle;
            Vector = PhysicsVector3.Zero;
            Position = position;
            Orientation = orientation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }
    }
}
