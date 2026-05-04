namespace helengine.editor {
    /// <summary>
    /// Creates isolated workspaces for platform build-graph executions.
    /// </summary>
    internal sealed class EditorPlatformBuildGraphWorkspaceFactory {
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

            string executionRootPath = Path.Combine(Path.GetTempPath(), "helengine-platform-build", platformId, queueItemId);
            return new EditorPlatformBuildGraphWorkspace(executionRootPath);
        }
    }
}
