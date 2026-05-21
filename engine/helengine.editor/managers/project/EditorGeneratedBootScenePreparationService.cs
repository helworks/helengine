namespace helengine.editor {
    /// <summary>
    /// Ensures generated boot-scene assets exist for platforms that route startup through SceneMapComponent.
    /// </summary>
    public sealed class EditorGeneratedBootScenePreparationService {
        /// <summary>
        /// Stable Windows platform id used by shared startup routing.
        /// </summary>
        const string WindowsPlatformId = "windows";

        /// <summary>
        /// Stable Nintendo DS platform id used by shared startup routing.
        /// </summary>
        const string NintendoDsPlatformId = "ds";

        /// <summary>
        /// Stable PlayStation 2 platform id used by shared startup routing.
        /// </summary>
        const string Playstation2PlatformId = "ps2";

        /// <summary>
        /// Stable PlayStation Portable platform id used by shared startup routing.
        /// </summary>
        const string PlaystationPortablePlatformId = "psp";

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
            } else if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            if (string.Equals(platformId, WindowsPlatformId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, Playstation2PlatformId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, PlaystationPortablePlatformId, StringComparison.OrdinalIgnoreCase)) {
                if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.DesktopMainMenuSceneId)
                    && !ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
                    return null;
                }

                return new Dictionary<string, string>(StringComparer.Ordinal);
            } else if (string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)) {
                if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId)
                    && !ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
                    return null;
                }

                Dictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.Ordinal) {
                    { PlatformMenuSceneResolver.DesktopMainMenuSceneId, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId }
                };
                AddNintendoDsCompanionSceneMappings(sceneIds, mappings);
                return mappings;
            }

            return null;
        }

        /// <summary>
        /// Adds Nintendo DS companion-scene mappings for selected scene ids that follow the generated DS companion naming contract.
        /// </summary>
        /// <param name="sceneIds">Stable scene ids selected for the build.</param>
        /// <param name="mappings">Scene remapping table receiving the derived companion mappings.</param>
        static void AddNintendoDsCompanionSceneMappings(IReadOnlyList<string> sceneIds, Dictionary<string, string> mappings) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            } else if (mappings == null) {
                throw new ArgumentNullException(nameof(mappings));
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                string selectedSceneId = sceneIds[index];
                if (!TryResolveNintendoDsCompanionSourceSceneId(selectedSceneId, out string sourceSceneId)) {
                    continue;
                } else if (!ContainsSceneId(sceneIds, sourceSceneId)) {
                    continue;
                } else if (mappings.ContainsKey(sourceSceneId)) {
                    continue;
                }

                mappings.Add(sourceSceneId, selectedSceneId);
            }
        }

        /// <summary>
        /// Resolves the default source scene id that corresponds to one generated Nintendo DS companion-scene id.
        /// </summary>
        /// <param name="companionSceneId">Nintendo DS companion-scene id to inspect.</param>
        /// <param name="sourceSceneId">Resolved default source scene id when the companion id matches the generated naming contract.</param>
        /// <returns>True when a default source scene id was resolved; otherwise false.</returns>
        static bool TryResolveNintendoDsCompanionSourceSceneId(string companionSceneId, out string sourceSceneId) {
            if (string.IsNullOrWhiteSpace(companionSceneId)) {
                sourceSceneId = string.Empty;
                return false;
            }

            const string dsIdSuffix = "_ds";
            if (!companionSceneId.Contains('/') && !companionSceneId.Contains('\\') && companionSceneId.EndsWith(dsIdSuffix, StringComparison.Ordinal)) {
                sourceSceneId = companionSceneId.Substring(0, companionSceneId.Length - dsIdSuffix.Length);
                return !string.IsNullOrWhiteSpace(sourceSceneId);
            }

            const string dsFolderSegment = "/ds/";
            const string dsSuffix = "_ds.helen";
            int folderIndex = companionSceneId.IndexOf(dsFolderSegment, StringComparison.Ordinal);
            if (folderIndex < 0 || !companionSceneId.EndsWith(dsSuffix, StringComparison.Ordinal)) {
                sourceSceneId = string.Empty;
                return false;
            }

            string prefix = companionSceneId.Substring(0, folderIndex + 1);
            string dsFileName = companionSceneId.Substring(folderIndex + dsFolderSegment.Length);
            string baseFileName = dsFileName.Substring(0, dsFileName.Length - dsSuffix.Length);
            sourceSceneId = prefix + baseFileName + ".helen";
            return true;
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
