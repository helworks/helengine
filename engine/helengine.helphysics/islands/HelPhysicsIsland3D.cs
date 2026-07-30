namespace helengine {
    /// <summary>
    /// Describes one non-empty contiguous range of ascending dynamic body indices in a builder's fixed member array.
    /// </summary>
    readonly struct HelPhysicsIsland3D {
        /// <summary>
        /// Stores the first flat member-array index owned by this island.
        /// </summary>
        public readonly int BodyStartIndex;

        /// <summary>
        /// Stores the positive number of dynamic bodies in this island's contiguous range.
        /// </summary>
        public readonly int BodyCount;

        /// <summary>
        /// Initializes one published island range after validating its non-negative start and positive member count.
        /// </summary>
        /// <param name="bodyStartIndex">First index in the flat island-member array.</param>
        /// <param name="bodyCount">Positive number of contiguous members.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the start is negative or the member count is not positive.</exception>
        public HelPhysicsIsland3D(int bodyStartIndex, int bodyCount) {
            if (bodyStartIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(bodyStartIndex), "Island member ranges cannot begin before index zero.");
            }

            if (bodyCount <= 0) {
                throw new ArgumentOutOfRangeException(nameof(bodyCount), "Published islands must contain at least one dynamic body.");
            }

            BodyStartIndex = bodyStartIndex;
            BodyCount = bodyCount;
        }
    }
}
