using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Creates one queued-build snapshot from a persisted platform configuration and a builder selection model.
    /// </summary>
    public sealed class EditorBuildQueueItemFactory {
        /// <summary>
        /// Stores the Windows platform id that forces the desktop demo-disc main menu to become the startup scene.
        /// </summary>
        const string WindowsPlatformId = "windows";

        /// <summary>
        /// Stores the project scene id that Windows builds must stage first as their startup scene.
        /// </summary>
        const string WindowsStartupSceneId = PlatformMenuSceneResolver.DesktopMainMenuSceneId;

        /// <summary>
        /// Stores the Nintendo DS platform id that forces the demo-disc main menu to become the startup scene.
        /// </summary>
        const string NintendoDsPlatformId = "ds";

        /// <summary>
        /// Stores the project scene id that Nintendo DS builds must stage first as their startup scene.
        /// </summary>
        const string NintendoDsStartupSceneId = PlatformMenuSceneResolver.NintendoDsMainMenuSceneId;

        /// <summary>
        /// Project scene catalog used to preserve stable scene ordering.
        /// </summary>
        readonly EditorProjectSceneCatalogService SceneCatalogService;

        /// <summary>
        /// Initializes one queue-item factory for the supplied project scene catalog.
        /// </summary>
        /// <param name="sceneCatalogService">Project scene catalog service.</param>
        public EditorBuildQueueItemFactory(EditorProjectSceneCatalogService sceneCatalogService) {
            SceneCatalogService = sceneCatalogService ?? throw new ArgumentNullException(nameof(sceneCatalogService));
        }

        /// <summary>
        /// Creates one immutable queued-build snapshot from the supplied platform state.
        /// </summary>
        /// <param name="platformConfig">Persisted platform configuration to capture.</param>
        /// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
        /// <param name="outputDirectoryPath">Build output directory path selected by the caller.</param>
        /// <returns>Queued build item ready for execution.</returns>
        public EditorBuildQueueItemDocument Create(
            EditorBuildPlatformConfigDocument platformConfig,
            EditorPlatformBuildSelectionModel selectionModel,
            string outputDirectoryPath) {
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
            EnsureSelectedScenes(platformConfig);
            List<string> orderedSceneIds = BuildOrderedSceneIds(platformConfig, platformConfig.SelectedSceneIds);
            ApplyPlatformStartupSceneOverrides(platformConfig.PlatformId, orderedSceneIds);
            if (orderedSceneIds.Count == 0) {
                throw new InvalidOperationException($"Platform '{platformConfig.PlatformId}' does not have any selected scenes.");
            }

            EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
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

            return queueItem;
        }

        /// <summary>
        /// Applies missing platform-selection defaults using the builder-provided profile metadata.
        /// </summary>
        /// <param name="platformConfig">Persisted platform configuration to normalize.</param>
        /// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
        void EnsurePlatformSelectionDefaults(EditorBuildPlatformConfigDocument platformConfig, EditorPlatformBuildSelectionModel selectionModel) {
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
        /// <param name="platformConfig">Platform configuration whose selected scenes should be normalized.</param>
        void EnsureSelectedScenes(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            platformConfig.SelectedSceneIds ??= [];
            platformConfig.SceneOrders ??= [];
            if (platformConfig.SelectedSceneIds.Count > 0 || platformConfig.SceneOrders.Count > 0) {
                return;
            }

            IReadOnlyList<string> sceneIds = SceneCatalogService.GetSceneIds();
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
        /// <param name="platformConfig">Platform configuration containing scene ordering values.</param>
        /// <param name="selectedSceneIds">Selected scene ids to order.</param>
        /// <returns>Ordered scene id list.</returns>
        List<string> BuildOrderedSceneIds(EditorBuildPlatformConfigDocument platformConfig, IReadOnlyList<string> selectedSceneIds) {
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

            IReadOnlyList<string> sceneIds = SceneCatalogService.GetSceneIds();
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
        /// Applies any platform-specific startup scene overrides to the ordered scene list.
        /// </summary>
        /// <param name="platformId">Platform identifier selected for the queued build.</param>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        void ApplyPlatformStartupSceneOverrides(string platformId, List<string> orderedSceneIds) {
            if (orderedSceneIds == null) {
                throw new ArgumentNullException(nameof(orderedSceneIds));
            }

            if (string.Equals(platformId, WindowsPlatformId, StringComparison.OrdinalIgnoreCase)) {
                EnsureStartupSceneFirst(orderedSceneIds, WindowsStartupSceneId);
                return;
            }

            if (!string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            EnsureStartupSceneFirst(orderedSceneIds, NintendoDsStartupSceneId);
        }

        /// <summary>
        /// Ensures platform startup-scene overrides always cook and stage the required scene first.
        /// </summary>
        /// <param name="orderedSceneIds">Ordered scene ids that will be cooked and packaged.</param>
        /// <param name="startupSceneId">Stable startup scene identifier that must be staged first.</param>
        void EnsureStartupSceneFirst(List<string> orderedSceneIds, string startupSceneId) {
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
