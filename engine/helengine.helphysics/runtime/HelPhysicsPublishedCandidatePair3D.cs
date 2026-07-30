namespace helengine {
    /// <summary>
    /// Captures both generations of one prior broadphase candidate so slot reuse cannot impersonate persistent contact potential.
    /// </summary>
    readonly struct HelPhysicsPublishedCandidatePair3D {
        /// <summary>
        /// Stores the complete pool identity of the lower-index candidate participant.
        /// </summary>
        public readonly HelPhysicsBodyHandle3D FirstBodyHandle;

        /// <summary>
        /// Stores the complete pool identity of the higher-index candidate participant.
        /// </summary>
        public readonly HelPhysicsBodyHandle3D SecondBodyHandle;

        /// <summary>
        /// Initializes one generation-safe prior candidate publication in canonical body-index order.
        /// </summary>
        /// <param name="firstBodyHandle">Current lower-index participant identity.</param>
        /// <param name="secondBodyHandle">Current higher-index participant identity.</param>
        public HelPhysicsPublishedCandidatePair3D(
            HelPhysicsBodyHandle3D firstBodyHandle,
            HelPhysicsBodyHandle3D secondBodyHandle) {
            FirstBodyHandle = firstBodyHandle;
            SecondBodyHandle = secondBodyHandle;
        }

        /// <summary>
        /// Determines whether both stored identities exactly match a current canonical pair.
        /// </summary>
        /// <param name="firstBodyHandle">Current lower-index participant identity.</param>
        /// <param name="secondBodyHandle">Current higher-index participant identity.</param>
        /// <returns><see langword="true"/> only when both indices and generations are unchanged.</returns>
        public bool Matches(
            HelPhysicsBodyHandle3D firstBodyHandle,
            HelPhysicsBodyHandle3D secondBodyHandle) {
            return FirstBodyHandle.Index == firstBodyHandle.Index &&
                FirstBodyHandle.Generation == firstBodyHandle.Generation &&
                SecondBodyHandle.Index == secondBodyHandle.Index &&
                SecondBodyHandle.Generation == secondBodyHandle.Generation;
        }
    }
}
