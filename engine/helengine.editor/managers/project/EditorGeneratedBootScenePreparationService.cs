namespace helengine.editor {
    /// <summary>
    /// Ensures generated boot-scene assets exist for platforms that route runtime scene loads through SceneMapComponent.
    /// </summary>
    public sealed class EditorGeneratedBootScenePreparationService {
        /// <summary>
        /// Environment variable that can override the generated boot-scene startup target for local build verification.
        /// </summary>
        const string GeneratedBootSceneInitialSceneIdEnvironmentVariable = "HELENGINE_GENERATED_BOOT_SCENE_INITIAL_SCENE_ID";

        /// <summary>
        /// Stable Nintendo DS platform identifier used by boot-scene remap generation.
        /// </summary>
        const string NintendoDsPlatformId = "ds";

        /// <summary>
        /// Stable Nintendo 3DS platform identifier that reuses Nintendo DS companion-scene remaps.
        /// </summary>
        const string Nintendo3DsPlatformId = "3ds";

        /// <summary>
        /// Stable suffix used by Nintendo DS companion scene ids.
        /// </summary>
        const string NintendoDsSceneSuffix = "_ds";

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
            }
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
            Dictionary<string, string> mappings = BuildMappings(platformId, sceneIds);
            if (mappings == null) {
                return;
            }

            string initialSceneId = ResolveInitialSceneId(sceneIds);
            SceneAsset sceneAsset = BootSceneAssetFactory.BuildSceneAsset(
                "Scenes/" + PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen",
                initialSceneId,
                mappings);
            string directoryPath = Path.GetDirectoryName(scenePath)
                ?? throw new InvalidOperationException("Generated boot scene path did not include a writable directory.");
            Directory.CreateDirectory(directoryPath);

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Resolves the startup scene id that should be written into the generated boot scene for the current build.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Startup scene id that should be requested after the generated boot scene loads.</returns>
        static string ResolveInitialSceneId(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            string overrideSceneId = Environment.GetEnvironmentVariable(GeneratedBootSceneInitialSceneIdEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(overrideSceneId)) {
                return PlatformMenuSceneResolver.DesktopMainMenuSceneId;
            } else if (!ContainsSceneId(sceneIds, overrideSceneId)) {
                throw new InvalidOperationException($"Generated boot scene startup override '{overrideSceneId}' is not present in the selected build scene set.");
            }

            return overrideSceneId;
        }

        /// <summary>
        /// Builds the authored boot-scene remapping table for one platform when startup should route through the helper scene.
        /// </summary>
        /// <param name="platformId">Target platform identifier.</param>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Authored remapping table, or null when the platform should not use the boot scene.</returns>
        static Dictionary<string, string> BuildMappings(string platformId, IReadOnlyList<string> sceneIds) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
                return null;
            }

            if (string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, Nintendo3DsPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return BuildNintendoDsMappings(sceneIds);
            }

            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the logical-scene remap table for Nintendo DS companion-scene selections.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <returns>Deterministic mapping from logical scene ids to Nintendo DS companion scene ids.</returns>
        static Dictionary<string, string> BuildNintendoDsMappings(IReadOnlyList<string> sceneIds) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            Dictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (ContainsSceneId(sceneIds, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId)) {
                mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId] = PlatformMenuSceneResolver.NintendoDsMainMenuSceneId;
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                string sceneId = sceneIds[index];
                if (string.IsNullOrWhiteSpace(sceneId)) {
                    continue;
                }
                if (!sceneId.EndsWith(NintendoDsSceneSuffix, StringComparison.Ordinal)) {
                    continue;
                }

                string logicalSceneId = sceneId.Substring(0, sceneId.Length - NintendoDsSceneSuffix.Length);
                if (string.IsNullOrWhiteSpace(logicalSceneId)) {
                    continue;
                }

                mappings[logicalSceneId] = sceneId;
            }

            return mappings;
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
    }
}
