namespace helengine.editor {
    /// <summary>
    /// Ensures generated platform menu scene assets already exist before build systems resolve selected scene ids to authored scene files.
    /// </summary>
    public sealed class EditorGeneratedMenuScenePreparationService {
        /// <summary>
        /// Absolute project root path that owns the source assets directory.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Project scene catalog used to resolve stable scene ids back to authored source paths.
        /// </summary>
        readonly EditorProjectSceneCatalogService SceneCatalogService;

        /// <summary>
        /// Initializes one generated menu-scene preparation service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver retained for constructor compatibility.</param>
        public EditorGeneratedMenuScenePreparationService(string projectRootPath, IScriptTypeResolver scriptTypeResolver) {
            ProjectRootPath = string.IsNullOrWhiteSpace(projectRootPath)
                ? throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath))
                : Path.GetFullPath(projectRootPath);
            SceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
        }

        /// <summary>
        /// Ensures any generated platform menu scenes referenced by the supplied build selection already exist on disk before later build phases run.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        public void EnsurePrepared(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId)) {
                return;
            }

            if (TryResolveExistingScenePath(PlatformMenuSceneResolver.NintendoDsMainMenuSceneId, out _)) {
                return;
            }

            throw new InvalidOperationException(
                $"Generated menu scene '{PlatformMenuSceneResolver.NintendoDsMainMenuSceneId}' is missing. Menu scene ownership now lives in the project, so the authored scene must already exist before the build runs.");
        }

        /// <summary>
        /// Resolves whether the supplied scene id is already part of the selected build set.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <param name="sceneId">Stable scene id to search for.</param>
        /// <returns>True when the scene id is present.</returns>
        static bool ContainsSceneId(IReadOnlyList<string> sceneIds, string sceneId) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                if (string.Equals(sceneIds[index], sceneId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to resolve one authored scene path for the supplied stable scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <param name="relativeScenePath">Resolved project-relative scene path when found.</param>
        /// <returns>True when the scene id resolved successfully.</returns>
        bool TryResolveExistingScenePath(string sceneId, out string relativeScenePath) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            try {
                relativeScenePath = SceneCatalogService.ResolveScenePath(sceneId);
                return true;
            } catch (InvalidOperationException) {
                relativeScenePath = string.Empty;
                return false;
            }
        }
    }
}
