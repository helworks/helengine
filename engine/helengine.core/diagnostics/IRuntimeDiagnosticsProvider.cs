namespace helengine {
    /// <summary>
    /// Supplies platform-specific runtime memory diagnostics snapshots to the shared core service.
    /// </summary>
    public interface IRuntimeDiagnosticsProvider {
        /// <summary>
        /// Captures the current platform diagnostics snapshot.
        /// </summary>
        /// <returns>A newly captured diagnostics snapshot whose cleanup responsibility transfers to the caller.</returns>
        [NativeOwnedReturn]
        RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot();
    }
}
