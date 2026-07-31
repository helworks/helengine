namespace helengine {
    /// <summary>
    /// Stores one persistent X-axis boundary used by the ordered sweep.
    /// </summary>
    struct HelPhysicsSweepEndpoint3D {
        /// <summary>
        /// Stores the X coordinate at which the represented body begins or ends its inclusive interval.
        /// </summary>
        public PhysicsScalar Value;

        /// <summary>
        /// Stores the stable body index whose interval owns this boundary.
        /// </summary>
        public int BodyIndex;

        /// <summary>
        /// Indicates whether this endpoint is an inclusive minimum rather than a maximum.
        /// </summary>
        public bool IsMinimum;
    }
}
