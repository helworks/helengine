namespace helengine.editor {
    /// <summary>
    /// Ensures generated boot-scene assets exist for platforms that route startup through SceneMapComponent.
    /// </summary>
    public sealed class EditorGeneratedBootScenePreparationService {
        /// <summary>
        /// Absolute project root path that owns the assets directory.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Generated boot-scene asset factory used to write the helper scene.
        /// </summary>
        readonly GeneratedBootSceneAssetFactory BootSceneAssetFactory;

        /// <summary>
        /// Initializes one generated boot-scene preparation service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorGeneratedBootScenePreparationService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            BootSceneAssetFactory = new GeneratedBootSceneAssetFactory();
        }

        /// <summary>
        /// Ensures the generated boot scene exists for the supplied platform and selected scene set.
        /// </summary>
        /// <param name="platformId">Target platform identifier.</param>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        public void EnsurePrepared(string platformId, IReadOnlyList<string> sceneIds) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            } else if (!string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                return;
            } else if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId)
                && !ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
                return;
            }

            SceneAsset sceneAsset = BootSceneAssetFactory.BuildSceneAsset(
                "Scenes/" + PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen",
                PlatformMenuSceneResolver.DesktopMainMenuSceneId,
                new Dictionary<string, string>(StringComparer.Ordinal) {
                    { PlatformMenuSceneResolver.DesktopMainMenuSceneId, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId }
                });
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
            string directoryPath = Path.GetDirectoryName(scenePath)
                ?? throw new InvalidOperationException("Generated boot scene path did not include a writable directory.");
            Directory.CreateDirectory(directoryPath);

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Resolves whether the supplied stable scene id is present in the selected build set.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <param name="sceneId">Scene id to search for.</param>
        /// <returns>True when the scene id is present.</returns>
        static bool ContainsSceneId(IReadOnlyList<string> sceneIds, string sceneId) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            } else if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                if (string.Equals(sceneIds[index], sceneId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }
    }
}
