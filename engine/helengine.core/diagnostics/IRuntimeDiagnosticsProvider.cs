namespace helengine {
    /// <summary>
    /// Supplies platform-specific runtime memory diagnostics snapshots to the shared core service.
    /// </summary>
    public interface IRuntimeDiagnosticsProvider {
        /// <summary>
        /// Captures the current platform diagnostics snapshot.
        /// </summary>
        /// <returns>Current platform diagnostics snapshot.</returns>
        RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot();
    }
}
