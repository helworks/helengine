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
        /// Initializes a body handle from its pool index and allocation generation.
        /// </summary>
        /// <param name="index">Fixed pool slot index, or <see cref="ushort.MaxValue"/> for an invalid handle.</param>
        /// <param name="generation">Generation current when the handle was issued.</param>
        public HelPhysicsBodyHandle3D(ushort index, ushort generation) {
            Index = index;
            Generation = generation;
        }
    }
}
