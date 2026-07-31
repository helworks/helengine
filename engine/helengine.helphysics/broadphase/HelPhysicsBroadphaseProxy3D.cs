namespace helengine {
    /// <summary>
    /// Stores one body's persistent broadphase metadata and world-space bounds in a fixed proxy slot.
    /// </summary>
    struct HelPhysicsBroadphaseProxy3D {
        /// <summary>
        /// Indicates whether this fixed proxy slot currently represents one body.
        /// </summary>
        public bool IsOccupied;

        /// <summary>
        /// Stores the stable body index represented by this proxy.
        /// </summary>
        public int BodyIndex;

        /// <summary>
        /// Stores how the body participates in simulation and broadphase candidacy.
        /// </summary>
        public BodyKind3D BodyKind;

        /// <summary>
        /// Stores awake state for dynamics or moved state for kinematics; static activity does not make pairs active.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// Stores the collision layer emitted by this body for other masks to inspect.
        /// </summary>
        public ushort CollisionLayer;

        /// <summary>
        /// Stores the collision layers this body permits for candidate generation.
        /// </summary>
        public ushort CollisionMask;

        /// <summary>
        /// Stores the body's current inclusive world-space broadphase bounds.
        /// </summary>
        public HelPhysicsAabb3D Aabb;
    }
}
