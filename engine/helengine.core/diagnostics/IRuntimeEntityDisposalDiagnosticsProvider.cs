namespace helengine {
    /// <summary>
    /// Receives live entity disposal notifications from hosts that need to locate hard hangs during scene teardown.
    /// </summary>
    public interface IRuntimeEntityDisposalDiagnosticsProvider {
        /// <summary>
        /// Reports one entity or component disposal boundary while scene root teardown is still executing.
        /// </summary>
        /// <param name="stage">Short disposal stage label.</param>
        /// <param name="entityChildCount">Current child count for the entity being disposed.</param>
        /// <param name="componentCount">Current component count for the entity being disposed.</param>
        /// <param name="componentIndex">Component index involved in the stage, or -1 when not component-specific.</param>
        void ReportEntityDisposalStage(string stage, int entityChildCount, int componentCount, int componentIndex);
    }
}
