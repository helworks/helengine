namespace helengine.editor {
    /// <summary>
    /// Represents the current task's project-scoped authoring transaction placeholder.
    /// </summary>
    public sealed class EditorAuthoringTransaction : IDisposable {
        /// <summary>
        /// Tracks whether this transaction has been disposed.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes one transaction owned by an authoring session.
        /// </summary>
        /// <param name="projectRootPath">Canonical project root associated with the transaction.</param>
        internal EditorAuthoringTransaction(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Gets the canonical project root associated with this transaction.
        /// </summary>
        internal string ProjectRootPath { get; }

        /// <summary>
        /// Releases the transaction placeholder and makes disposal idempotent.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
        }
    }
}
