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
            NormalizeSelectedScenes(sceneCatalogService, platformConfig);
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

            PlatformBuildProfileDefinition previousBuildProfile = selectionModel.TryResolveBuildProfileExact(platformConfig.SelectedBuildProfileId);
            PlatformBuildProfileDefinition buildProfile = EditorBuildProfileDefaultResolver.ResolveBuildProfile(
                selectionModel,
                platformConfig.SelectedBuildProfileId,
                platformConfig.DebugBuild);
            if (buildProfile != null) {
                platformConfig.SelectedBuildProfileId = buildProfile.ProfileId;
                string selectedGraphicsProfileId = platformConfig.SelectedGraphicsProfileId;
                EditorBuildProfileDefaultResolver.SynchronizeBoundProfileSelection(
                    ref selectedGraphicsProfileId,
                    previousBuildProfile?.GraphicsProfileId ?? string.Empty,
                    buildProfile.GraphicsProfileId);
                platformConfig.SelectedGraphicsProfileId = selectedGraphicsProfileId;
                string selectedCodegenProfileId = platformConfig.SelectedCodegenProfileId;
                EditorBuildProfileDefaultResolver.SynchronizeBoundProfileSelection(
                    ref selectedCodegenProfileId,
                    previousBuildProfile?.CodegenProfileId ?? string.Empty,
                    buildProfile.CodegenProfileId);
                platformConfig.SelectedCodegenProfileId = selectedCodegenProfileId;

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
                platformConfig.SelectedCodegenOptionValues = EditorBuildProfileDefaultResolver.CreateEffectiveCodegenOptionValues(
                    platformConfig.SelectedCodegenOptionValues,
                    codegenProfile,
                    previousBuildProfile,
                    buildProfile);
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
        /// Removes stale scene selections and remaps handheld companion-scene ids back to their canonical authored scene ids.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog used to validate authored scene ids.</param>
        /// <param name="platformConfig">Platform configuration whose persisted scene selection should be normalized.</param>
        static void NormalizeSelectedScenes(EditorProjectSceneCatalogService sceneCatalogService, EditorBuildPlatformConfigDocument platformConfig) {
            if (sceneCatalogService == null) {
                throw new ArgumentNullException(nameof(sceneCatalogService));
            }
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            platformConfig.SelectedSceneIds ??= [];
            platformConfig.SceneOrders ??= [];

            HashSet<string> availableSceneIds = new HashSet<string>(sceneCatalogService.GetSceneIds(), StringComparer.Ordinal);
            List<string> normalizedSelectedSceneIds = new List<string>(platformConfig.SelectedSceneIds.Count);
            HashSet<string> selectedSceneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < platformConfig.SelectedSceneIds.Count; index++) {
                string normalizedSceneId = NormalizeSelectedSceneId(platformConfig.PlatformId, platformConfig.SelectedSceneIds[index], availableSceneIds);
                if (string.IsNullOrWhiteSpace(normalizedSceneId)) {
                    continue;
                }
                if (!IsKnownBuildSceneId(availableSceneIds, normalizedSceneId)) {
                    continue;
                }
                if (selectedSceneIds.Add(normalizedSceneId)) {
                    normalizedSelectedSceneIds.Add(normalizedSceneId);
                }
            }

            List<EditorBuildSceneOrderDocument> normalizedSceneOrders = new List<EditorBuildSceneOrderDocument>(platformConfig.SceneOrders.Count);
            HashSet<string> orderedSceneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                if (sceneOrder == null) {
                    continue;
                }

                string normalizedSceneId = NormalizeSelectedSceneId(platformConfig.PlatformId, sceneOrder.SceneId, availableSceneIds);
                if (string.IsNullOrWhiteSpace(normalizedSceneId)) {
                    continue;
                }
                if (!selectedSceneIds.Contains(normalizedSceneId)) {
                    continue;
                }
                if (!orderedSceneIds.Add(normalizedSceneId)) {
                    continue;
                }

                normalizedSceneOrders.Add(new EditorBuildSceneOrderDocument {
                    SceneId = normalizedSceneId,
                    OrderNumber = sceneOrder.OrderNumber
                });
            }

            platformConfig.SelectedSceneIds = normalizedSelectedSceneIds;
            platformConfig.SceneOrders = normalizedSceneOrders;
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

            PlatformBuildProfileDefinition buildProfile = EditorBuildProfileDefaultResolver.ResolveBuildProfile(
                selectionModel,
                platformConfig.SelectedBuildProfileId,
                platformConfig.DebugBuild);
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
            _ = sceneCatalogService;
            _ = platformId;
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
        /// Applies any shared startup-scene ordering rules to the ordered scene list.
        /// </summary>
        /// <param name="platformId">Platform identifier selected for the queued build.</param>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        static void ApplyPlatformStartupSceneOverrides(string platformId, List<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            if (string.Equals(platformId, "ps2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "3ds", StringComparison.OrdinalIgnoreCase)) {
                orderedSceneIds.RemoveAll(sceneId => string.Equals(sceneId, PlatformMenuSceneResolver.GeneratedBootSceneId, StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Resolves whether one scene id can legally appear in a queued build selection.
        /// </summary>
        /// <param name="availableSceneIds">Authored scene ids currently present in the project catalog.</param>
        /// <param name="sceneId">Scene id to validate.</param>
        /// <returns>True when the scene id is authored or is the generated boot scene helper id.</returns>
        static bool IsKnownBuildSceneId(HashSet<string> availableSceneIds, string sceneId) {
            if (availableSceneIds == null) {
                throw new ArgumentNullException(nameof(availableSceneIds));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                return false;
            }

            if (string.Equals(sceneId, PlatformMenuSceneResolver.GeneratedBootSceneId, StringComparison.Ordinal)) {
                return true;
            }

            return availableSceneIds.Contains(sceneId);
        }

        /// <summary>
        /// Remaps one persisted scene id to the canonical authored scene id required by the active platform.
        /// </summary>
        /// <param name="platformId">Platform identifier selected for the queued build.</param>
        /// <param name="sceneId">Persisted scene id to normalize.</param>
        /// <param name="availableSceneIds">Authored scene ids currently present in the project catalog.</param>
        /// <returns>Canonical scene id to keep, or the original scene id when no remap is required.</returns>
        static string NormalizeSelectedSceneId(string platformId, string sceneId, HashSet<string> availableSceneIds) {
            if (availableSceneIds == null) {
                throw new ArgumentNullException(nameof(availableSceneIds));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                return string.Empty;
            }

            if (!IsNintendoHandheldPlatform(platformId)) {
                return sceneId;
            }
            if (string.Equals(sceneId, PlatformMenuSceneResolver.DesktopMainMenuSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, PlatformMenuSceneResolver.NintendoDsMainMenuSceneId, StringComparison.Ordinal)
                || string.Equals(sceneId, PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId, StringComparison.Ordinal)) {
                return PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId;
            }
            if (!sceneId.EndsWith("_ds", StringComparison.Ordinal)) {
                return sceneId;
            }

            string canonicalSceneId = sceneId.Substring(0, sceneId.Length - 3);
            if (string.IsNullOrWhiteSpace(canonicalSceneId)) {
                return sceneId;
            }
            if (!availableSceneIds.Contains(canonicalSceneId)) {
                return sceneId;
            }

            return canonicalSceneId;
        }

        /// <summary>
        /// Resolves whether one platform now consumes canonical scenes directly instead of handheld companion-scene ids.
        /// </summary>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the platform should remap stale handheld companion-scene ids.</returns>
        static bool IsNintendoHandheldPlatform(string platformId) {
            return string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "3ds", StringComparison.OrdinalIgnoreCase);
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
