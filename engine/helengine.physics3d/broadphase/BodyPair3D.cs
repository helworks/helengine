namespace helengine {
    /// <summary>
    /// Stores one candidate broadphase pair as indices into the dense body-state list.
    /// </summary>
    public readonly struct BodyPair3D {
        /// <summary>
        /// Initializes one candidate broadphase pair.
        /// </summary>
        /// <param name="firstBodyIndex">Lower dense body-state index.</param>
        /// <param name="secondBodyIndex">Higher dense body-state index.</param>
        public BodyPair3D(int firstBodyIndex, int secondBodyIndex) {
            if (firstBodyIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(firstBodyIndex), "Body indices must be non-negative.");
            }
            if (secondBodyIndex <= firstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(secondBodyIndex), "The second body index must be greater than the first body index.");
            }

            FirstBodyIndex = firstBodyIndex;
            SecondBodyIndex = secondBodyIndex;
        }

        /// <summary>
        /// Gets the lower dense body-state index.
        /// </summary>
        public int FirstBodyIndex { get; }

        /// <summary>
        /// Gets the higher dense body-state index.
        /// </summary>
        public int SecondBodyIndex { get; }
    }
}
