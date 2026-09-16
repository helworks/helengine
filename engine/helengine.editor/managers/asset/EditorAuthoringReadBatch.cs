namespace helengine.editor {
    /// <summary>
    /// Reuses one verified mutation scope per directory for the duration of a read-only batch, such as the
    /// boot-time identity reconcile. Each verified read otherwise re-verifies the whole directory chain, which
    /// costs about 25 times a plain read; a project with thousands of assets in under a hundred directories
    /// spends seconds on that alone. Scopes stay verified for the batch's lifetime and are released on dispose.
    /// </summary>
    internal sealed class EditorAuthoringReadBatch : IDisposable {
        /// <summary>
        /// Batch active on the current thread, or null when verified reads should acquire their own scope.
        /// </summary>
        [ThreadStatic]
        static EditorAuthoringReadBatch current;

        /// <summary>
        /// Path comparison used for directory keys.
        /// </summary>
        static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// Normalized project root this batch serves.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Batch that was active when this one began, restored on dispose.
        /// </summary>
        readonly EditorAuthoringReadBatch Previous;

        /// <summary>
        /// Verified scopes keyed by full directory path.
        /// </summary>
        readonly Dictionary<string, EditorAuthoringMutationScope> ScopesByDirectory = new Dictionary<string, EditorAuthoringMutationScope>(PathComparer);

        /// <summary>
        /// Whether the batch has been disposed.
        /// </summary>
        bool IsDisposed;

        EditorAuthoringReadBatch(string projectRootPath, EditorAuthoringReadBatch previous) {
            ProjectRootPath = projectRootPath;
            Previous = previous;
        }

        /// <summary>
        /// Begins a read batch for one project on the current thread. Nested batches for the same root share
        /// the outer batch's scopes.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root the reads belong to.</param>
        /// <returns>Batch to dispose when the read-only work completes.</returns>
        internal static EditorAuthoringReadBatch Begin(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            EditorAuthoringReadBatch batch = new EditorAuthoringReadBatch(NormalizeRoot(projectRootPath), current);
            current = batch;
            return batch;
        }

        /// <summary>
        /// Returns the verified scope to use for a directory when a batch for the same project root is active.
        /// </summary>
        /// <param name="projectRootPath">Project root the read belongs to.</param>
        /// <param name="directoryPath">Full directory containing the file to read.</param>
        /// <returns>Cached verified scope, or null when no matching batch is active.</returns>
        internal static EditorAuthoringMutationScope TryGetScope(string projectRootPath, string directoryPath) {
            EditorAuthoringReadBatch batch = current;
            while (batch != null) {
                if (!batch.IsDisposed && PathComparer.Equals(batch.ProjectRootPath, NormalizeRoot(projectRootPath))) {
                    return batch.IsPinnable(directoryPath) ? batch.GetOrAcquireScope(directoryPath) : null;
                }

                batch = batch.Previous;
            }

            return null;
        }

        /// <summary>
        /// Only directories inside the project's assets tree are pinned. Writers stage and rename temporary
        /// directories under the cache folder, and an open handle on one of those would make the rename fail.
        /// </summary>
        /// <param name="directoryPath">Directory a read wants to pin.</param>
        /// <returns>True when the directory is the assets root or lies beneath it.</returns>
        bool IsPinnable(string directoryPath) {
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                return false;
            }

            string fullDirectoryPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsRootPath = Path.Combine(ProjectRootPath, "assets");
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(fullDirectoryPath, assetsRootPath, comparison)
                || fullDirectoryPath.StartsWith(assetsRootPath + Path.DirectorySeparatorChar, comparison);
        }

        /// <summary>
        /// Pins a directory through the active batch, verifying and holding its whole chain once, so callers can
        /// limit their own reparse checks to the leaf entry.
        /// </summary>
        /// <param name="projectRootPath">Project root the directory belongs to.</param>
        /// <param name="directoryPath">Full directory to pin.</param>
        /// <returns>True when a matching batch is active and the directory chain is now verified and pinned.</returns>
        internal static bool TryPinDirectory(string projectRootPath, string directoryPath) {
            // Acquiring a scope creates missing directories; a validation-only pin must never do that.
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return false;
            }

            return TryGetScope(projectRootPath, directoryPath) != null;
        }

        /// <summary>
        /// Releases every cached scope and restores the previously active batch.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
            if (ReferenceEquals(current, this)) {
                current = Previous;
            }

            foreach (EditorAuthoringMutationScope scope in ScopesByDirectory.Values) {
                scope.Dispose();
            }

            ScopesByDirectory.Clear();
        }

        EditorAuthoringMutationScope GetOrAcquireScope(string directoryPath) {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!ScopesByDirectory.TryGetValue(fullDirectoryPath, out EditorAuthoringMutationScope scope)) {
                scope = EditorAuthoringMutationScope.AcquireForMutation(ProjectRootPath, fullDirectoryPath);
                ScopesByDirectory.Add(fullDirectoryPath, scope);
            }

            return scope;
        }

        static string NormalizeRoot(string projectRootPath) {
            return Path.GetFullPath(projectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
