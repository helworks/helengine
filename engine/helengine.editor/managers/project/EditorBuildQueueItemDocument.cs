using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Represents one persisted queued build entry stored in `user_settings/build_config.json`.
    /// </summary>
    public sealed class EditorBuildQueueItemDocument {
        /// <summary>
        /// Gets or sets the stable queue item identifier used to track this entry across reloads.
        /// </summary>
        public string QueueItemId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the platform identifier this queued build targets.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project-relative scene identifiers selected for this queued build.
        /// </summary>
        public List<string> SelectedSceneIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the user-selected output directory path for this queued build.
        /// </summary>
        public string OutputDirectoryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the persisted execution state for this queued build item.
        /// </summary>
        public EditorBuildQueueItemStatus Status { get; set; } = EditorBuildQueueItemStatus.Pending;

        /// <summary>
        /// Gets or sets the human-readable status detail associated with the current queue item state.
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the persisted debug-build snapshot captured when the queue item was created.
        /// </summary>
        public bool DebugBuild { get; set; }

        /// <summary>
        /// Gets or sets how the queued build should finish after the normal export/package phases complete.
        /// </summary>
        public EditorBuildExecutionMode ExecutionMode { get; set; } = EditorBuildExecutionMode.Runtime;

        /// <summary>
        /// Gets or sets the selected builder-provided build profile id.
        /// </summary>
        public string SelectedBuildProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided graphics profile id.
        /// </summary>
        public string SelectedGraphicsProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided build option values.
        /// </summary>
        public Dictionary<string, string> SelectedBuildOptionValues { get; set; } = [];

        /// <summary>
        /// Gets or sets the selected builder-provided graphics option values.
        /// </summary>
        public Dictionary<string, string> SelectedGraphicsOptionValues { get; set; } = [];

        /// <summary>
        /// Gets or sets the selected builder-provided codegen profile id.
        /// </summary>
        public string SelectedCodegenProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided storage profile id.
        /// </summary>
        public string SelectedStorageProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided media profile id.
        /// </summary>
        public string SelectedMediaProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected builder-provided codegen option values.
        /// </summary>
        public Dictionary<string, string> SelectedCodegenOptionValues { get; set; } = [];

        /// <summary>
        /// Creates one queued-build snapshot from persisted platform configuration and builder metadata.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to seed and order scene selections.</param>
        /// <param name="platformConfig">Persisted platform configuration to capture.</param>
        /// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
        /// <param name="outputDirectoryPath">Build output directory path selected by the caller.</param>
        /// <returns>Queued build item ready for execution.</returns>
        public static EditorBuildQueueItemDocument Create(
            EditorProjectSceneCatalogService sceneCatalogService,
            EditorBuildPlatformConfigDocument platformConfig,
            EditorPlatformBuildSelectionModel selectionModel,
            string outputDirectoryPath) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }

            EnsurePlatformSelectionDefaults(platformConfig, selectionModel);
            EnsureSelectedScenes(sceneCatalogService, platformConfig);
            List<string> orderedSceneIds = BuildOrderedSceneIds(sceneCatalogService, platformConfig, platformConfig.SelectedSceneIds);
            ApplyPlatformSceneExpansions(sceneCatalogService, platformConfig.PlatformId, orderedSceneIds);
            ApplyPlatformStartupSceneOverrides(platformConfig.PlatformId, orderedSceneIds);
            if (orderedSceneIds.Count == 0) {
                throw new InvalidOperationException($"Platform '{platformConfig.PlatformId}' does not have any selected scenes.");
            }

            return new EditorBuildQueueItemDocument {
                QueueItemId = Guid.NewGuid().ToString("N"),
                PlatformId = platformConfig.PlatformId,
                SelectedSceneIds = orderedSceneIds,
                OutputDirectoryPath = Path.GetFullPath(outputDirectoryPath),
                DebugBuild = platformConfig.DebugBuild,
                ExecutionMode = EditorBuildExecutionMode.Runtime,
                SelectedBuildProfileId = platformConfig.SelectedBuildProfileId,
                SelectedGraphicsProfileId = platformConfig.SelectedGraphicsProfileId,
                SelectedBuildOptionValues = new Dictionary<string, string>(platformConfig.SelectedBuildOptionValues ?? new Dictionary<string, string>()),
                SelectedGraphicsOptionValues = new Dictionary<string, string>(platformConfig.SelectedGraphicsOptionValues ?? new Dictionary<string, string>()),
                SelectedCodegenProfileId = platformConfig.SelectedCodegenProfileId,
                SelectedStorageProfileId = platformConfig.SelectedStorageProfileId,
                SelectedMediaProfileId = platformConfig.SelectedMediaProfileId,
                SelectedCodegenOptionValues = new Dictionary<string, string>(platformConfig.SelectedCodegenOptionValues ?? new Dictionary<string, string>())
            };
        }

        /// <summary>
        /// Applies missing platform-selection defaults using the builder-provided profile metadata.
        /// </summary>
        /// <param name="platformConfig">Persisted platform configuration to normalize.</param>
        /// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
        static void EnsurePlatformSelectionDefaults(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(platformConfig, selectionModel);
            if (buildProfile != null) {
                platformConfig.SelectedBuildProfileId = buildProfile.ProfileId;
                if (string.IsNullOrWhiteSpace(platformConfig.SelectedGraphicsProfileId)) {
                    platformConfig.SelectedGraphicsProfileId = buildProfile.GraphicsProfileId;
                }
                if (string.IsNullOrWhiteSpace(platformConfig.SelectedCodegenProfileId)) {
                    platformConfig.SelectedCodegenProfileId = buildProfile.CodegenProfileId;
                }

                EnsureSettingDefaults(platformConfig.SelectedBuildOptionValues, buildProfile.Settings);
            }

            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platformConfig, buildProfile, selectionModel);
            if (graphicsProfile != null) {
                platformConfig.SelectedGraphicsProfileId = graphicsProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedGraphicsOptionValues, graphicsProfile.Settings);
            }

            PlatformCodegenProfileDefinition codegenProfile = ResolveCodegenProfile(platformConfig, buildProfile, selectionModel);
            if (codegenProfile != null) {
                platformConfig.SelectedCodegenProfileId = codegenProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedCodegenOptionValues, codegenProfile.Settings);
            }

            PlatformStorageProfileDefinition storageProfile = ResolveStorageProfile(platformConfig, selectionModel);
            if (storageProfile != null) {
                platformConfig.SelectedStorageProfileId = storageProfile.ProfileId;
            }

            PlatformMediaProfileDefinition mediaProfile = ResolveMediaProfile(platformConfig, selectionModel);
            if (mediaProfile != null) {
                platformConfig.SelectedMediaProfileId = mediaProfile.ProfileId;
            }

            platformConfig.SelectedBuildOptionValues ??= new Dictionary<string, string>();
            platformConfig.SelectedGraphicsOptionValues ??= new Dictionary<string, string>();
            platformConfig.SelectedCodegenOptionValues ??= new Dictionary<string, string>();
        }

        /// <summary>
        /// Seeds a blank platform scene selection from the project scene catalog so a first build can proceed.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to enumerate available scene ids.</param>
        /// <param name="platformConfig">Platform configuration whose selected scenes should be normalized.</param>
        static void EnsureSelectedScenes(EditorProjectSceneCatalogService sceneCatalogService, EditorBuildPlatformConfigDocument platformConfig) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            platformConfig.SelectedSceneIds ??= [];
            platformConfig.SceneOrders ??= [];
            if (platformConfig.SelectedSceneIds.Count > 0 || platformConfig.SceneOrders.Count > 0) {
                return;
            }

            IReadOnlyList<string> sceneIds = sceneCatalogService.GetSceneIds();
            for (int index = 0; index < sceneIds.Count; index++) {
                platformConfig.SelectedSceneIds.Add(sceneIds[index]);
            }
        }

        /// <summary>
        /// Resolves the selected build profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided selection model.</param>
        /// <returns>Resolved build profile metadata.</returns>
        static PlatformBuildProfileDefinition ResolveBuildProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            PlatformBuildProfileDefinition buildProfile = selectionModel.ResolveBuildProfile(platformConfig.SelectedBuildProfileId);
            if (buildProfile != null) {
                return buildProfile;
            }

            return selectionModel.ResolveBuildProfile(string.Empty);
        }

        /// <summary>
        /// Resolves the selected graphics profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata.</param>
        /// <param name="selectionModel">Builder-provided selection model.</param>
        /// <returns>Resolved graphics profile metadata.</returns>
        static PlatformGraphicsProfileDefinition ResolveGraphicsProfile(EditorBuildPlatformConfigDocument platformConfig, PlatformBuildProfileDefinition buildProfile, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            string graphicsProfileId = platformConfig.SelectedGraphicsProfileId;
            if (string.IsNullOrWhiteSpace(graphicsProfileId) && buildProfile != null) {
                graphicsProfileId = buildProfile.GraphicsProfileId;
            }

            PlatformGraphicsProfileDefinition graphicsProfile = selectionModel.ResolveGraphicsProfile(graphicsProfileId);
            if (graphicsProfile != null) {
                return graphicsProfile;
            }

            return selectionModel.ResolveGraphicsProfile(string.Empty);
        }

        /// <summary>
        /// Resolves the selected codegen profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata.</param>
        /// <param name="selectionModel">Builder-provided selection model.</param>
        /// <returns>Resolved codegen profile metadata.</returns>
        static PlatformCodegenProfileDefinition ResolveCodegenProfile(EditorBuildPlatformConfigDocument platformConfig, PlatformBuildProfileDefinition buildProfile, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            string codegenProfileId = platformConfig.SelectedCodegenProfileId;
            if (string.IsNullOrWhiteSpace(codegenProfileId) && buildProfile != null) {
                codegenProfileId = buildProfile.CodegenProfileId;
            }

            PlatformCodegenProfileDefinition codegenProfile = selectionModel.ResolveCodegenProfile(codegenProfileId);
            if (codegenProfile != null) {
                return codegenProfile;
            }

            return selectionModel.ResolveCodegenProfile(string.Empty);
        }

        /// <summary>
        /// Resolves the selected storage profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided selection model.</param>
        /// <returns>Resolved storage profile metadata.</returns>
        static PlatformStorageProfileDefinition ResolveStorageProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            PlatformStorageProfileDefinition storageProfile = selectionModel.ResolveStorageProfile(platformConfig.SelectedStorageProfileId);
            if (storageProfile != null) {
                return storageProfile;
            }

            return selectionModel.ResolveStorageProfile(string.Empty);
        }

        /// <summary>
        /// Resolves the selected media profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="selectionModel">Builder-provided selection model.</param>
        /// <returns>Resolved media profile metadata.</returns>
        static PlatformMediaProfileDefinition ResolveMediaProfile(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectionModel == null) {
                throw new ArgumentNullException(nameof(selectionModel));
            }

            PlatformMediaProfileDefinition mediaProfile = selectionModel.ResolveMediaProfile(platformConfig.SelectedMediaProfileId);
            if (mediaProfile != null) {
                return mediaProfile;
            }

            return selectionModel.ResolveMediaProfile(string.Empty);
        }

        /// <summary>
        /// Seeds any missing option values from the supplied setting collection.
        /// </summary>
        /// <param name="values">Persisted option values.</param>
        /// <param name="settings">Builder-provided setting definitions.</param>
        static void EnsureSettingDefaults(Dictionary<string, string> values, PlatformSettingDefinition[] settings) {
            if (values == null || settings == null) {
                return;
            }

            for (int index = 0; index < settings.Length; index++) {
                PlatformSettingDefinition setting = settings[index];
                if (!values.TryGetValue(setting.SettingId, out string existingValue) || string.IsNullOrWhiteSpace(existingValue)) {
                    values[setting.SettingId] = setting.DefaultValue;
                }
            }
        }

        /// <summary>
        /// Sorts selected scene ids by the persisted scene-order values.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to stabilize fallback order.</param>
        /// <param name="platformConfig">Platform configuration containing scene ordering values.</param>
        /// <param name="selectedSceneIds">Selected scene ids to order.</param>
        /// <returns>Ordered scene id list.</returns>
        static List<string> BuildOrderedSceneIds(EditorProjectSceneCatalogService sceneCatalogService, EditorBuildPlatformConfigDocument platformConfig, IReadOnlyList<string> selectedSceneIds) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (selectedSceneIds == null) {
                throw new ArgumentNullException(nameof(selectedSceneIds));
            }

            List<string> orderedSceneIds = new List<string>(selectedSceneIds.Count);
            for (int index = 0; index < selectedSceneIds.Count; index++) {
                orderedSceneIds.Add(selectedSceneIds[index]);
            }

            IReadOnlyList<string> sceneIds = sceneCatalogService.GetSceneIds();
            orderedSceneIds.Sort((leftSceneId, rightSceneId) => {
                int leftOrderNumber = GetSceneOrderNumber(platformConfig, leftSceneId, sceneIds);
                int rightOrderNumber = GetSceneOrderNumber(platformConfig, rightSceneId, sceneIds);
                int orderComparison = leftOrderNumber.CompareTo(rightOrderNumber);
                if (orderComparison != 0) {
                    return orderComparison;
                }

                int leftSceneIndex = IndexOf(sceneIds, leftSceneId);
                int rightSceneIndex = IndexOf(sceneIds, rightSceneId);
                return leftSceneIndex.CompareTo(rightSceneIndex);
            });

            return orderedSceneIds;
        }

        /// <summary>
        /// Applies platform-specific scene-set expansions that must happen before the build item is persisted.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to resolve authored scene paths.</param>
        /// <param name="platformId">Platform identifier selected for the queued build.</param>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        static void ApplyPlatformSceneExpansions(EditorProjectSceneCatalogService sceneCatalogService, string platformId, List<string> orderedSceneIds) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (!string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            ApplyNintendoDsCompanionSceneExpansions(sceneCatalogService, orderedSceneIds);
        }

        /// <summary>
        /// Expands one Nintendo DS scene set so authored DS companion scenes cook beside their default authored source scenes.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to resolve authored scene paths.</param>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        static void ApplyNintendoDsCompanionSceneExpansions(EditorProjectSceneCatalogService sceneCatalogService, List<string> orderedSceneIds) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            IReadOnlyList<string> sceneCatalogIds = sceneCatalogService.GetSceneIds();
            HashSet<string> sceneCatalogIdSet = new HashSet<string>(sceneCatalogIds, StringComparer.Ordinal);
            List<string> expandedSceneIds = new List<string>(orderedSceneIds.Count);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                if (!TryResolveNintendoDsCompanionSceneId(sceneCatalogService, sceneId, sceneCatalogIdSet, out string companionSceneId)) {
                    if (IndexOf(expandedSceneIds, sceneId) < 0) {
                        expandedSceneIds.Add(sceneId);
                    }
                    continue;
                }
                if (IndexOf(expandedSceneIds, companionSceneId) < 0) {
                    expandedSceneIds.Add(companionSceneId);
                }
                if (IndexOf(expandedSceneIds, sceneId) < 0) {
                    expandedSceneIds.Add(sceneId);
                }
            }

            orderedSceneIds.Clear();
            orderedSceneIds.AddRange(expandedSceneIds);
        }

        /// <summary>
        /// Resolves the authored Nintendo DS companion-scene id for one default authored scene when the companion exists.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to resolve authored scene paths.</param>
        /// <param name="sceneId">Default authored scene id selected for the build.</param>
        /// <param name="sceneCatalogIdSet">Project scene ids currently present in the catalog.</param>
        /// <param name="companionSceneId">Resolved companion-scene id when the authored DS scene exists.</param>
        /// <returns>True when the selected scene has an authored DS companion scene.</returns>
        static bool TryResolveNintendoDsCompanionSceneId(EditorProjectSceneCatalogService sceneCatalogService, string sceneId, ISet<string> sceneCatalogIdSet, out string companionSceneId) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (sceneCatalogIdSet == null) {
                throw new ArgumentNullException(nameof(sceneCatalogIdSet));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                companionSceneId = string.Empty;
                return false;
            }

            string authoredScenePath = NormalizeScenePath(sceneCatalogService.ResolveScenePath(sceneId));
            if (IsNintendoDsCompanionScenePath(authoredScenePath)) {
                companionSceneId = string.Empty;
                return false;
            }

            string directoryPath = Path.GetDirectoryName(authoredScenePath)?.Replace('\\', '/') ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(authoredScenePath);
            string sceneExtension = Path.GetExtension(authoredScenePath);
            string companionScenePath = string.IsNullOrWhiteSpace(directoryPath)
                ? "ds/" + fileNameWithoutExtension + "_ds" + sceneExtension
                : directoryPath + "/ds/" + fileNameWithoutExtension + "_ds" + sceneExtension;
            companionSceneId = SceneIdUtility.FromPath(companionScenePath);
            return sceneCatalogIdSet.Contains(companionSceneId);
        }

        /// <summary>
        /// Resolves whether one authored scene path already targets a Nintendo DS companion-scene file.
        /// </summary>
        /// <param name="scenePath">Project-relative authored scene path.</param>
        /// <returns>True when the path already points at a Nintendo DS companion scene.</returns>
        static bool IsNintendoDsCompanionScenePath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return false;
            }

            string normalizedScenePath = NormalizeScenePath(scenePath);
            return normalizedScenePath.Contains("/ds/", StringComparison.Ordinal)
                && normalizedScenePath.EndsWith("_ds.helen", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes one authored scene path to forward slashes so naming-contract checks remain stable across hosts.
        /// </summary>
        /// <param name="scenePath">Project-relative authored scene path.</param>
        /// <returns>Normalized project-relative authored scene path.</returns>
        static string NormalizeScenePath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            return scenePath.Replace('\\', '/');
        }

        /// <summary>
        /// Applies platform-specific startup scene overrides to the ordered scene list.
        /// </summary>
        /// <param name="platformId">Platform identifier selected for the queued build.</param>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        static void ApplyPlatformStartupSceneOverrides(string platformId, List<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (!string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            if (!RequiresGeneratedBootScene(orderedSceneIds)) {
                return;
            }

            EnsureStartupSceneFirst(orderedSceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId);
        }

        /// <summary>
        /// Resolves whether the selected scene set needs generated boot-scene routing for demo-disc menu startup.
        /// </summary>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        /// <returns>True when the scene set includes the desktop menu or already selected the generated boot scene.</returns>
        static bool RequiresGeneratedBootScene(IReadOnlyList<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            return IndexOf(orderedSceneIds, PlatformMenuSceneResolver.DesktopMainMenuSceneId) >= 0
                || IndexOf(orderedSceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId) >= 0;
        }

        /// <summary>
        /// Ensures one startup scene always cooks and stages first.
        /// </summary>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        /// <param name="startupSceneId">Stable startup scene identifier that must be staged first.</param>
        static void EnsureStartupSceneFirst(List<string> orderedSceneIds, string startupSceneId) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }
            if (string.IsNullOrWhiteSpace(startupSceneId)) {
                throw new ArgumentException("Startup scene id must be provided.", nameof(startupSceneId));
            }

            int startupSceneIndex = IndexOf(orderedSceneIds, startupSceneId);
            if (startupSceneIndex < 0) {
                orderedSceneIds.Insert(0, startupSceneId);
                return;
            }
            if (startupSceneIndex == 0) {
                return;
            }

            orderedSceneIds.RemoveAt(startupSceneIndex);
            orderedSceneIds.Insert(0, startupSceneId);
        }

        /// <summary>
        /// Reads the persisted ordering number for one scene, falling back to the catalog order when needed.
        /// </summary>
        /// <param name="platformConfig">Platform configuration containing the saved ordering values.</param>
        /// <param name="sceneId">Project-relative scene identifier whose order should be resolved.</param>
        /// <param name="sceneIds">Current project scene catalog.</param>
        /// <returns>1-based ordering number for the requested scene.</returns>
        static int GetSceneOrderNumber(EditorBuildPlatformConfigDocument platformConfig, string sceneId, IReadOnlyList<string> sceneIds) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                return int.MaxValue;
            }

            if (platformConfig.SceneOrders != null) {
                for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                    EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                    if (sceneOrder != null && sceneOrder.SceneId == sceneId && sceneOrder.OrderNumber > 0) {
                        return sceneOrder.OrderNumber;
                    }
                }
            }

            int sceneIndex = IndexOf(sceneIds, sceneId);
            if (sceneIndex >= 0) {
                return sceneIndex + 1;
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Finds one scene identifier in the supplied catalog.
        /// </summary>
        /// <param name="sceneIds">Scene catalog to inspect.</param>
        /// <param name="sceneId">Scene identifier to locate.</param>
        /// <returns>Zero-based scene index, or -1 when absent.</returns>
        static int IndexOf(IReadOnlyList<string> sceneIds, string sceneId) {
            if (sceneIds == null || string.IsNullOrWhiteSpace(sceneId)) {
                return -1;
            }

            for (int index = 0; index < sceneIds.Count; index++) {
                if (string.Equals(sceneIds[index], sceneId, StringComparison.Ordinal)) {
                    return index;
                }
            }

            return -1;
        }
    }
}
