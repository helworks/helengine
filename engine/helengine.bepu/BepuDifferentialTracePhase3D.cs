namespace helengine {
    /// <summary>
    /// Identifies one reduced-BEPU simulation boundary captured by the native-versus-managed differential harness.
    /// </summary>
    public enum BepuDifferentialTracePhase3D {
        /// <summary>
        /// Captures one velocity-integration callback observation before solver work continues.
        /// </summary>
        IntegrateVelocityCallback,

        /// <summary>
        /// Captures one integration-responsibility assignment observation during constrained scheduling.
        /// </summary>
        IntegrationResponsibilityAssignment,

        /// <summary>
        /// Captures one constrained gather/integrate snapshot before velocity integration mutates body state.
        /// </summary>
        GatherAndIntegrateBefore,

        /// <summary>
        /// Captures one constrained gather/integrate snapshot after velocity integration mutates body state.
        /// </summary>
        GatherAndIntegrateAfter,

        /// <summary>
        /// Captures one two-body solver snapshot before contact solving mutates gathered velocities.
        /// </summary>
        TwoBodySolveBefore,

        /// <summary>
        /// Captures one two-body solver snapshot after contact solving mutates gathered velocities.
        /// </summary>
        TwoBodySolveAfter,

        /// <summary>
        /// Captures one synchronized body snapshot after runtime state has been written back into the scene.
        /// </summary>
        SyncSnapshot
    }
}
