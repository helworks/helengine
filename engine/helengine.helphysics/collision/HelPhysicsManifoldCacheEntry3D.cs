namespace helengine {
    /// <summary>
    /// Stores one fixed-table manifold cache slot, including its pair identity, persisted contacts, lifecycle step, and probe state.
    /// </summary>
    struct HelPhysicsManifoldCacheEntry3D {
        /// <summary>
        /// Stores the canonical body pair that owns this slot while the slot is occupied.
        /// </summary>
        public HelPhysicsPairKey3D Pair;

        /// <summary>
        /// Stores the most recently retained contact geometry and solver state for the owning pair.
        /// </summary>
        public HelPhysicsContactManifold3D Manifold;

        /// <summary>
        /// Stores the simulation step that most recently updated or touched the owning pair.
        /// </summary>
        public int StepId;

        /// <summary>
        /// Stores the explicit empty, occupied, or tombstone state used to preserve open-addressing probe chains.
        /// </summary>
        public byte State;
    }
}
