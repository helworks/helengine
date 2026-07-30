namespace helengine {
    /// <summary>
    /// Stores one allocation-free deferred world mutation with a generation-safe body identity and optional vector input.
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
        /// Stores a world-space force or impulse and remains zero for lifecycle commands.
        /// </summary>
        public readonly PhysicsVector3 Vector;

        /// <summary>
        /// Initializes one complete command value for deterministic insertion-order execution.
        /// </summary>
        /// <param name="kind">Mutation represented by this command.</param>
        /// <param name="bodyHandle">Current pool-internal body identity targeted by the mutation.</param>
        /// <param name="vector">World-space force or impulse, or zero for lifecycle commands.</param>
        public HelPhysicsDeferredCommand3D(
            HelPhysicsDeferredCommandKind3D kind,
            HelPhysicsBodyHandle3D bodyHandle,
            PhysicsVector3 vector) {
            Kind = kind;
            BodyHandle = bodyHandle;
            Vector = vector;
        }
    }
}
