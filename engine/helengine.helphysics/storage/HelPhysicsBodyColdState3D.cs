namespace helengine {
    /// <summary>
    /// Stores body metadata that simulation hot loops do not need to read while integrating motion.
    /// </summary>
    struct HelPhysicsBodyColdState3D {
        /// <summary>
        /// Stores the separate shape allocation that defines this body's collision geometry.
        /// </summary>
        public HelPhysicsShapeHandle3D ShapeHandle;

        /// <summary>
        /// Stores how the body participates in simulation and collision response.
        /// </summary>
        public BodyKind3D BodyKind;

        /// <summary>
        /// Stores validated contact-response coefficients without requiring a runtime material lookup.
        /// </summary>
        public HelPhysicsMaterial3D Material;

        /// <summary>
        /// Stores the collision layer this body belongs to.
        /// </summary>
        public ushort CollisionLayer;

        /// <summary>
        /// Stores the collision layers this body accepts.
        /// </summary>
        public ushort CollisionMask;

        /// <summary>
        /// Stores the authored entity binding used to connect simulation results back to engine ownership.
        /// </summary>
        public int EntityBindingId;
    }
}
