namespace helengine.editor {
    /// <summary>
    /// Ensures generated boot-scene assets exist for platforms that route runtime scene loads through SceneMapComponent.
    /// </summary>
    public sealed class EditorGeneratedBootScenePreparationService {
        /// <summary>
        /// Stable Nintendo DS platform id used by scene-companion routing.
        /// </summary>
        const string NintendoDsPlatformId = "ds";
        /// <summary>
        /// Stable Nintendo 3DS platform id used by the DS companion-scene routing contract.
        /// </summary>
        const string Nintendo3DsPlatformId = "3ds";

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

            Dictionary<string, string> mappings = BuildMappings(platformId, sceneIds);
            if (mappings == null) {
                return;
            }

            SceneAsset sceneAsset = BootSceneAssetFactory.BuildSceneAsset(
                "Scenes/" + PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen",
                PlatformMenuSceneResolver.DesktopMainMenuSceneId,
                mappings);
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
            string directoryPath = Path.GetDirectoryName(scenePath)
                ?? throw new InvalidOperationException("Generated boot scene path did not include a writable directory.");
            Directory.CreateDirectory(directoryPath);

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
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
            if (!UsesNintendoDsCompanionSceneMappings(platformId)) {
                return null;
            }
            if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
                return null;
            }

            Dictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.Ordinal);
            AddNintendoDsCompanionSceneMappings(sceneIds, mappings);
            return mappings;
        }

        /// <summary>
        /// Resolves whether one platform should route generated boot-scene startup through the Nintendo DS companion-scene mapping contract.
        /// </summary>
        /// <param name="platformId">Target platform identifier to inspect.</param>
        /// <returns>True when the platform should use Nintendo DS companion-scene mappings; otherwise false.</returns>
        static bool UsesNintendoDsCompanionSceneMappings(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, Nintendo3DsPlatformId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds Nintendo DS companion-scene mappings for selected scene ids that follow the generated companion naming contract.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <param name="mappings">Scene remapping table receiving the derived companion mappings.</param>
        static void AddNintendoDsCompanionSceneMappings(IReadOnlyList<string> sceneIds, Dictionary<string, string> mappings) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (mappings == null) {
                throw new ArgumentNullException(nameof(mappings));
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                string selectedSceneId = sceneIds[index];
                if (!TryResolveNintendoDsCompanionSourceSceneId(selectedSceneId, out string sourceSceneId)) {
                    continue;
                }
                if (mappings.ContainsKey(sourceSceneId)) {
                    continue;
                }

                mappings.Add(sourceSceneId, selectedSceneId);
            }
        }

        /// <summary>
        /// Resolves the default source scene id that corresponds to one Nintendo DS companion-scene id.
        /// </summary>
        /// <param name="companionSceneId">Nintendo DS companion-scene id to inspect.</param>
        /// <param name="sourceSceneId">Resolved default source scene id when the companion id matches the naming contract.</param>
        /// <returns>True when a default source scene id was resolved; otherwise false.</returns>
        static bool TryResolveNintendoDsCompanionSourceSceneId(string companionSceneId, out string sourceSceneId) {
            if (string.IsNullOrWhiteSpace(companionSceneId)) {
                sourceSceneId = string.Empty;
                return false;
            }

            const string SnakeCaseDsIdSuffix = "_ds";
            const string PascalCaseDsIdSuffix = "Ds";
            if (companionSceneId.Contains('/') || companionSceneId.Contains('\\')) {
                sourceSceneId = string.Empty;
                return false;
            }
            if (companionSceneId.EndsWith(SnakeCaseDsIdSuffix, StringComparison.Ordinal)) {
                sourceSceneId = companionSceneId.Substring(0, companionSceneId.Length - SnakeCaseDsIdSuffix.Length);
                return !string.IsNullOrWhiteSpace(sourceSceneId);
            }
            if (companionSceneId.EndsWith(PascalCaseDsIdSuffix, StringComparison.Ordinal)) {
                sourceSceneId = companionSceneId.Substring(0, companionSceneId.Length - PascalCaseDsIdSuffix.Length);
                return !string.IsNullOrWhiteSpace(sourceSceneId);
            }

            sourceSceneId = string.Empty;
            return false;
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
