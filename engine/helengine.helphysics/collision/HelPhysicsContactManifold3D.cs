namespace helengine {
    /// <summary>
    /// Stores up to four box contact points inline so narrow-phase and solver traversal require no per-manifold array.
    /// </summary>
    struct HelPhysicsContactManifold3D {
        /// <summary>
        /// Stores the first inline contact slot.
        /// </summary>
        public HelPhysicsContactPoint3D Contact0;

        /// <summary>
        /// Stores the second inline contact slot.
        /// </summary>
        public HelPhysicsContactPoint3D Contact1;

        /// <summary>
        /// Stores the third inline contact slot.
        /// </summary>
        public HelPhysicsContactPoint3D Contact2;

        /// <summary>
        /// Stores the fourth inline contact slot.
        /// </summary>
        public HelPhysicsContactPoint3D Contact3;

        /// <summary>
        /// Stores how many leading inline contact slots contain the current manifold.
        /// </summary>
        public int ContactCount;

        /// <summary>
        /// Returns one inline contact slot after validating the fixed manifold capacity.
        /// </summary>
        /// <param name="contactIndex">Inline slot index from zero through three.</param>
        /// <returns>The contact currently stored in the selected slot.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contactIndex"/> is outside the four inline slots.</exception>
        public HelPhysicsContactPoint3D GetContact(int contactIndex) {
            if (contactIndex == 0) {
                return Contact0;
            } else if (contactIndex == 1) {
                return Contact1;
            } else if (contactIndex == 2) {
                return Contact2;
            } else if (contactIndex == 3) {
                return Contact3;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Manifold contacts are indexed from zero through three.");
        }

        /// <summary>
        /// Replaces one inline contact slot after validating the fixed manifold capacity.
        /// </summary>
        /// <param name="contactIndex">Inline slot index from zero through three.</param>
        /// <param name="contact">Contact value to store in the selected slot.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contactIndex"/> is outside the four inline slots.</exception>
        public void SetContact(int contactIndex, in HelPhysicsContactPoint3D contact) {
            if (contactIndex == 0) {
                Contact0 = contact;
            } else if (contactIndex == 1) {
                Contact1 = contact;
            } else if (contactIndex == 2) {
                Contact2 = contact;
            } else if (contactIndex == 3) {
                Contact3 = contact;
            } else {
                throw new ArgumentOutOfRangeException(nameof(contactIndex), "Manifold contacts are indexed from zero through three.");
            }
        }

        /// <summary>
        /// Clears the active count and every inline slot before a manifold is rebuilt or rejected.
        /// </summary>
        public void Reset() {
            Contact0 = default;
            Contact1 = default;
            Contact2 = default;
            Contact3 = default;
            ContactCount = 0;
        }
    }
}
