namespace helengine.editor {
    /// <summary>
    /// Records each resource acquired during session construction in acquisition
    /// order. A failed construction attempts every cleanup action in reverse
    /// order and preserves all failures so a later owner cannot be stranded by
    /// an earlier disposal error.
    /// </summary>
    internal sealed class EditorSessionConstructionLedger {
        readonly List<Action> CleanupActions = new List<Action>();
        bool Transferred;

        internal void Register(object resource) {
            if (resource is IDisposable disposable) {
                Register(disposable.Dispose);
            }
        }

        internal void Register(Action cleanup) {
            if (cleanup == null) {
                throw new ArgumentNullException(nameof(cleanup));
            }
            if (Transferred) {
                throw new InvalidOperationException("The editor session construction ledger has already transferred ownership.");
            }
            CleanupActions.Add(cleanup);
        }

        internal void TransferOwnership() {
            Transferred = true;
            CleanupActions.Clear();
        }

        /// <summary>
        /// Replaces construction-only registrations with the single teardown
        /// action owned by the successfully constructed session.
        /// </summary>
        /// <param name="ownerCleanup">Aggregate teardown action for the session.</param>
        internal void TransferOwnership(Action ownerCleanup) {
            if (ownerCleanup == null) {
                throw new ArgumentNullException(nameof(ownerCleanup));
            }

            CleanupActions.Clear();
            CleanupActions.Add(ownerCleanup);
            Transferred = true;
        }

        internal void Dispose() {
            List<Exception> failures = new List<Exception>();
            for (int index = CleanupActions.Count - 1; index >= 0; index--) {
                try {
                    CleanupActions[index]();
                    // Retain only actions that failed. A second cleanup pass
                    // must retry the unresolved owner without invoking already
                    // completed teardown actions a second time.
                    CleanupActions.RemoveAt(index);
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }
            if (failures.Count == 1) {
                throw failures[0];
            }
            if (failures.Count > 1) {
                throw new AggregateException("Editor session construction cleanup failed.", failures);
            }
        }
    }
}
