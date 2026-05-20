namespace helengine {
    /// <summary>
    /// Receives low-level core update stage notifications from hosts that need live diagnostics while an update is still executing.
    /// </summary>
    public interface IRuntimeUpdateStageDiagnosticsProvider {
        /// <summary>
        /// Reports the next core update stage before that stage runs so platform hosts can diagnose hard hangs that never return to the host loop.
        /// </summary>
        /// <param name="stage">Short stage label describing the next core update subsystem.</param>
        void ReportUpdateStage(string stage);
    }
}
