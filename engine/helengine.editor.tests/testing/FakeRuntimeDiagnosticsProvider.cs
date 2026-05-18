namespace helengine.editor.tests.testing {
    /// <summary>
    /// Supplies a fixed runtime diagnostics snapshot for core diagnostics tests.
    /// </summary>
    public sealed class FakeRuntimeDiagnosticsProvider : IRuntimeDiagnosticsProvider, IRuntimeMemoryCounterProvider {
        /// <summary>
        /// Snapshot returned by each capture call.
        /// </summary>
        readonly RuntimeMemoryDiagnosticsSnapshot SnapshotValue;

        /// <summary>
        /// Reusable scalar counters mirrored from the configured snapshot.
        /// </summary>
        readonly RuntimeMemoryCounters CountersValue;

        /// <summary>
        /// Tracks how many full snapshot captures have been requested.
        /// </summary>
        int SnapshotCaptureCountValue;

        /// <summary>
        /// Tracks how many lightweight counter captures have been requested.
        /// </summary>
        int MemoryCounterCaptureCountValue;

        /// <summary>
        /// Initializes one fake provider with a fixed snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot returned by each capture call.</param>
        public FakeRuntimeDiagnosticsProvider(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            SnapshotValue = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            CountersValue = new RuntimeMemoryCounters();
            CountersValue.CopyFromSnapshot(snapshot);
        }

        /// <summary>
        /// Gets how many times callers requested a full diagnostics snapshot.
        /// </summary>
        public int SnapshotCaptureCount {
            get { return SnapshotCaptureCountValue; }
        }

        /// <summary>
        /// Gets how many times callers requested lightweight scalar counters.
        /// </summary>
        public int MemoryCounterCaptureCount {
            get { return MemoryCounterCaptureCountValue; }
        }

        /// <summary>
        /// Returns the configured fixed snapshot.
        /// </summary>
        /// <returns>Configured fixed runtime diagnostics snapshot.</returns>
        public RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot() {
            SnapshotCaptureCountValue++;
            return SnapshotValue;
        }

        /// <summary>
        /// Copies the configured scalar counters into the supplied reusable container.
        /// </summary>
        /// <param name="counters">Reusable counter container that should receive the configured values.</param>
        public void CaptureMemoryCounters(RuntimeMemoryCounters counters) {
            if (counters == null) {
                throw new ArgumentNullException(nameof(counters));
            }

            MemoryCounterCaptureCountValue++;
            counters.CopyFromSnapshot(SnapshotValue);
        }
    }
}
