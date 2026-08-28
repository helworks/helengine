namespace helengine.editor {
    /// <summary>
    /// Represents one independently retryable teardown operation. It is also
    /// safe for callers that must retire an owned object before the session's
    /// final ledger pass, such as scale-sensitive dialog recreation.
    /// </summary>
    internal sealed class EditorSessionCleanupItem {
        readonly Action Cleanup;
        bool Completed;

        internal EditorSessionCleanupItem(Action cleanup) {
            Cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        internal void Execute() {
            if (Completed) {
                return;
            }

            Cleanup();
            Completed = true;
        }
    }

    /// <summary>
    /// Groups editor-session teardown actions so detachment and process-wide
    /// state reset always run before owned resources are released.
    /// </summary>
    internal enum EditorSessionCleanupPhase {
        Dispose = 0,
        OwnedState = 1,
        Panel = 2,
        Reset = 3,
        Detach = 4
    }

    /// <summary>
    /// Records resources acquired during construction and retains the same
    /// individual actions for the live session after ownership transfer.
    /// Successful actions are completed permanently; failed actions remain
    /// available for a later retry.
    /// </summary>
    internal sealed class EditorSessionConstructionLedger {
        sealed class CleanupEntry {
            internal readonly Action Cleanup;
            internal readonly EditorSessionCleanupPhase Phase;
            internal bool Completed;

            internal CleanupEntry(Action cleanup, EditorSessionCleanupPhase phase) {
                Cleanup = cleanup;
                Phase = phase;
            }
        }

        readonly List<CleanupEntry> CleanupEntries = new List<CleanupEntry>();
        bool Transferred;

        /// <summary>
        /// Optional test seam invoked immediately before each unresolved
        /// cleanup item. A throwing callback leaves that item pending while
        /// allowing all later items to be attempted.
        /// </summary>
        internal Action<int> BeforeCleanupAction { get; set; }

        internal void Register(object resource) {
            Register(resource, EditorSessionCleanupPhase.Dispose);
        }

        internal void Register(object resource, EditorSessionCleanupPhase phase) {
            if (resource is IDisposable disposable) {
                Register(disposable.Dispose, phase);
            }
        }

        internal void Register(Action cleanup) {
            Register(cleanup, EditorSessionCleanupPhase.Dispose);
        }

        internal void Register(Action cleanup, EditorSessionCleanupPhase phase) {
            if (cleanup == null) {
                throw new ArgumentNullException(nameof(cleanup));
            }

            // Dynamic workspace factories add resources to this same ledger
            // after construction has transferred ownership.
            CleanupEntries.Add(new CleanupEntry(cleanup, phase));
        }

        /// <summary>
        /// Transfers all individually tracked entries to the live session.
        /// </summary>
        internal void TransferOwnership() {
            Transferred = true;
        }

        internal bool HasTransferredOwnership => Transferred;

        /// <summary>
        /// Attempts every unresolved entry. Higher-priority phases run first,
        /// while entries within a phase retain reverse acquisition order.
        /// </summary>
        internal void Dispose() {
            List<Exception> failures = new List<Exception>();
            int cleanupActionIndex = 0;
            for (int phaseValue = (int)EditorSessionCleanupPhase.Detach; phaseValue >= (int)EditorSessionCleanupPhase.Dispose; phaseValue--) {
                EditorSessionCleanupPhase phase = (EditorSessionCleanupPhase)phaseValue;
                for (int index = CleanupEntries.Count - 1; index >= 0; index--) {
                    CleanupEntry entry = CleanupEntries[index];
                    if (entry.Completed || entry.Phase != phase) {
                        continue;
                    }

                    try {
                        BeforeCleanupAction?.Invoke(cleanupActionIndex++);
                        entry.Cleanup();
                        entry.Completed = true;
                    } catch (Exception exception) {
                        failures.Add(exception);
                    }
                }
            }
            if (failures.Count == 1) {
                throw failures[0];
            }
            if (failures.Count > 1) {
                throw new AggregateException("Editor session cleanup failed; retry disposal to complete cleanup.", failures);
            }
        }
    }
}
