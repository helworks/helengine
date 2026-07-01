namespace helengine.editor {
    /// <summary>
    /// Creates isolated workspaces for platform build-graph executions.
    /// </summary>
    internal sealed class EditorPlatformBuildGraphWorkspaceFactory {
        /// <summary>
        /// Central resolver used to place workspace roots beneath the project-scoped platform isolation tree.
        /// </summary>
        readonly EditorBuildIsolationPathResolver IsolationPathResolver;

        /// <summary>
        /// Initializes one workspace factory for the supplied authored project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root path.</param>
        public EditorPlatformBuildGraphWorkspaceFactory(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            IsolationPathResolver = new EditorBuildIsolationPathResolver(projectRootPath);
        }

        /// <summary>
        /// Creates one workspace for the supplied platform id and queue item.
        /// </summary>
        public EditorPlatformBuildGraphWorkspace Create(string platformId, string queueItemId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id must be provided.", nameof(queueItemId));
            }

            string executionRootPath = IsolationPathResolver.ResolveWorkspaceExecutionRootPath(platformId, queueItemId);
            return new EditorPlatformBuildGraphWorkspace(executionRootPath);
        }
    }
}
