namespace helengine {
    /// <summary>
    /// Identifies one deterministic broadphase candidate using canonical ascending body-index order.
    /// </summary>
    public readonly struct HelPhysicsCandidatePair3D {
        /// <summary>
        /// Stores the lower body index in the candidate pair.
        /// </summary>
        public readonly int FirstBodyIndex;

        /// <summary>
        /// Stores the higher body index in the candidate pair.
        /// </summary>
        public readonly int SecondBodyIndex;

        /// <summary>
        /// Initializes a canonical candidate pair from two distinct ascending body indices.
        /// </summary>
        /// <param name="firstBodyIndex">Lower non-negative body index.</param>
        /// <param name="secondBodyIndex">Higher body index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is negative or the indices are not ascending and distinct.</exception>
        public HelPhysicsCandidatePair3D(int firstBodyIndex, int secondBodyIndex) {
            if (firstBodyIndex < 0 || secondBodyIndex <= firstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(secondBodyIndex), "Candidate pair indices must be non-negative, distinct, and ascending.");
            }

            FirstBodyIndex = firstBodyIndex;
            SecondBodyIndex = secondBodyIndex;
        }
    }
}
