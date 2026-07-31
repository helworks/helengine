namespace helengine {
    /// <summary>
    /// Identifies one body slot and the generation that proves the slot has not since been recycled.
    /// </summary>
    public readonly struct HelPhysicsBodyHandle3D {
        /// <summary>
        /// Stores the fixed body-pool slot index, with <see cref="ushort.MaxValue"/> reserved as invalid.
        /// </summary>
        public readonly ushort Index;

        /// <summary>
        /// Stores the slot generation issued when this handle was allocated.
        /// </summary>
        public readonly ushort Generation;

        /// <summary>
        /// Stores the world ownership token for public handles, with zero reserved for pool-internal identities.
        /// </summary>
        public readonly uint WorldId;

        /// <summary>
        /// Initializes a body handle from its pool index and allocation generation.
        /// </summary>
        /// <param name="index">Fixed pool slot index, or <see cref="ushort.MaxValue"/> for an invalid handle.</param>
        /// <param name="generation">Generation current when the handle was issued.</param>
        public HelPhysicsBodyHandle3D(ushort index, ushort generation) {
            Index = index;
            Generation = generation;
            WorldId = 0;
        }

        /// <summary>
        /// Initializes a public body handle from its pool identity and the world that owns it.
        /// </summary>
        /// <param name="index">Fixed pool slot index, or <see cref="ushort.MaxValue"/> for an invalid handle.</param>
        /// <param name="generation">Generation current when the handle was issued.</param>
        /// <param name="worldId">Nonzero ownership token assigned by the creating world.</param>
        public HelPhysicsBodyHandle3D(ushort index, ushort generation, uint worldId) {
            Index = index;
            Generation = generation;
            WorldId = worldId;
        }
    }
}
