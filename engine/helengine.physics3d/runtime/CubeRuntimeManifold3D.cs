namespace helengine {
    /// <summary>
    /// Associates a cube contact manifold with the dense body indices that produced it.
    /// </summary>
    public sealed class CubeRuntimeManifold3D {
        /// <summary>
        /// Initializes one runtime manifold record.
        /// </summary>
        /// <param name="firstBodyIndex">Dense index of the first cube body.</param>
        /// <param name="secondBodyIndex">Dense index of the second cube body.</param>
        /// <param name="contact">Resolved cube contact manifold.</param>
        public CubeRuntimeManifold3D(int firstBodyIndex, int secondBodyIndex, CubeContactManifold3D contact) {
            if (firstBodyIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(firstBodyIndex), "Body indices must be non-negative.");
            }
            if (secondBodyIndex <= firstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(secondBodyIndex), "The second body index must be greater than the first body index.");
            }

            FirstBodyIndex = firstBodyIndex;
            SecondBodyIndex = secondBodyIndex;
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
        }

        /// <summary>
        /// Gets the dense index of the first cube body.
        /// </summary>
        public int FirstBodyIndex { get; }

        /// <summary>
        /// Gets the dense index of the second cube body.
        /// </summary>
        public int SecondBodyIndex { get; }

        /// <summary>
        /// Gets the resolved cube contact manifold.
        /// </summary>
        public CubeContactManifold3D Contact { get; }
    }
}
