namespace helengine.editor.tests.testing {
    /// <summary>
    /// Supplies a fixed runtime diagnostics snapshot for core diagnostics tests.
    /// </summary>
    public sealed class FakeRuntimeDiagnosticsProvider : IRuntimeDiagnosticsProvider {
        /// <summary>
        /// Snapshot returned by each capture call.
        /// </summary>
        readonly RuntimeMemoryDiagnosticsSnapshot SnapshotValue;

        /// <summary>
        /// Initializes one fake provider with a fixed snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot returned by each capture call.</param>
        public FakeRuntimeDiagnosticsProvider(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            SnapshotValue = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>
        /// Returns the configured fixed snapshot.
        /// </summary>
        /// <returns>Configured fixed runtime diagnostics snapshot.</returns>
        public RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot() {
            return SnapshotValue;
        }
    }
}
