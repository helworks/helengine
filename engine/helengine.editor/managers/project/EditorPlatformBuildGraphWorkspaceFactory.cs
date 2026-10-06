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
        /// Authored project root used to place direct-editor native caches inside the project.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one workspace factory for the supplied authored project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root path.</param>
        public EditorPlatformBuildGraphWorkspaceFactory(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            IsolationPathResolver = new EditorBuildIsolationPathResolver(ProjectRootPath);
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

            string executionRootPath = IsolationPathResolver.ResolveWorkspaceExecutionRootPath(platformId, queueItemId, Guid.NewGuid().ToString("N"));
            if (IsolationPathResolver.UsesStableCacheRoot()) {
                if (string.Equals(platformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                    return new EditorPlatformBuildGraphWorkspace(
                        executionRootPath,
                        IsolationPathResolver.ResolveNativeRootPath(platformId));
                }

                return new EditorPlatformBuildGraphWorkspace(
                    executionRootPath,
                    IsolationPathResolver.ResolveGeneratedCoreRootPath(platformId),
                    IsolationPathResolver.ResolveNativeRootPath(platformId));
            }

            return new EditorPlatformBuildGraphWorkspace(executionRootPath);
        }

        /// <summary>
        /// Creates a disposable graph workspace with persistent generated-source and native-object roots for one profile.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <param name="buildProfileId">Selected build profile identifier.</param>
        /// <param name="queueItemId">Queued build item identifier used to isolate disposable graph state.</param>
        /// <returns>Workspace with invocation-owned graph paths and stable project/profile cache paths.</returns>
        public EditorPlatformBuildGraphWorkspace Create(string platformId, string buildProfileId, string queueItemId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(buildProfileId)) {
                throw new ArgumentException("Build profile id must be provided.", nameof(buildProfileId));
            } else if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id must be provided.", nameof(queueItemId));
            }

            string executionRootPath = IsolationPathResolver.ResolveWorkspaceExecutionRootPath(platformId, queueItemId, Guid.NewGuid().ToString("N"));
            string cacheRootPath = IsolationPathResolver.UsesStableCacheRoot()
                ? IsolationPathResolver.ResolveStableProfileCacheRootPath(platformId)
                : EditorNativeBuildCache.ResolveCacheRootPath(ProjectRootPath, platformId, buildProfileId);
            string nativeObjectCacheRootPath = Path.Combine(cacheRootPath, "native");
            return new EditorPlatformBuildGraphWorkspace(
                executionRootPath,
                Path.Combine(executionRootPath, "generated-core"),
                Path.Combine(executionRootPath, "builder"),
                nativeObjectCacheRootPath);
        }
    }
}
