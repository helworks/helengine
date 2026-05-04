namespace helengine {
    /// <summary>
    /// Defines one broadphase strategy that reduces rigid-body contact work to plausible candidate pairs.
    /// </summary>
    public interface IBroadphase3D {
        /// <summary>
        /// Collects the candidate body pairs that should be inspected by the narrowphase and solver.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        /// <returns>Candidate body pairs that may overlap or interact this step.</returns>
        IReadOnlyList<BodyPair3D> CollectCandidatePairs(IReadOnlyList<BodyState3D> bodyStates);
    }
}
