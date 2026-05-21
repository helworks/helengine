namespace helengine {
    /// <summary>
    /// Stores one candidate cube pair as stable indices into the dense cube body list.
    /// </summary>
    public readonly struct CubeBodyPair3D {
        /// <summary>
        /// Initializes one candidate pair.
        /// </summary>
        /// <param name="firstBodyIndex">Lower dense cube body index.</param>
        /// <param name="secondBodyIndex">Higher dense cube body index.</param>
        public CubeBodyPair3D(int firstBodyIndex, int secondBodyIndex) {
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
        /// Gets the lower dense cube body index.
        /// </summary>
        public int FirstBodyIndex { get; }

        /// <summary>
        /// Gets the higher dense cube body index.
        /// </summary>
        public int SecondBodyIndex { get; }
    }
}
