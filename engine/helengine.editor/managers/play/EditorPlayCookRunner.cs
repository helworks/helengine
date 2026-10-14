namespace helengine.editor {
    /// <summary>
    /// Runs the assets-only Play cook for the active platform through the platform build graph. It snapshots the
    /// platform's persisted build settings into a queue item exactly like a regular build, then narrows the scene
    /// selection to the scene open in the editor, so Play cooks and starts only that scene. The staged output lives
    /// under <c>&lt;project&gt;/cache/play/&lt;platformId&gt;/output</c>.
    /// </summary>
    public sealed class EditorPlayCookRunner : IEditorPlayCookRunner {
        /// <summary>
        /// Project folder that holds disposable editor caches.
        /// </summary>
        const string CacheFolderName = "cache";

        /// <summary>
        /// Folder beneath the cache that holds per-platform Play cook output.
        /// </summary>
        const string PlayFolderName = "play";

        /// <summary>
        /// Folder beneath one platform's Play cache that the queue item names as its output directory.
        /// </summary>
        const string OutputFolderName = "output";

        /// <summary>
        /// Absolute authored project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Project scene catalog used to seed the queue item and to resolve the open scene's id.
        /// </summary>
        readonly EditorProjectSceneCatalogService SceneCatalogService;

        /// <summary>
        /// Resolves the persisted build settings of one platform.
        /// </summary>
        readonly Func<string, EditorBuildPlatformConfigDocument> PlatformConfigResolver;

        /// <summary>
        /// Resolves the builder-provided selection model of one platform.
        /// </summary>
        readonly Func<string, EditorPlatformBuildSelectionModel> SelectionModelResolver;

        /// <summary>
        /// Creates the platform build executor that runs the cook.
        /// </summary>
        readonly Func<string, IEditorPlayAssetCookExecutor> ExecutorFactory;

        /// <summary>
        /// Supplies the saved path of the scene open in the editor.
        /// </summary>
        readonly Func<string> CurrentScenePathProvider;

        /// <summary>
        /// Platform of the queue item the latest <see cref="Cook"/> created, or null before the first cook and while a
        /// cook is creating its queue item.
        /// </summary>
        string CookedPlatformId;

        /// <summary>
        /// Environment captured by the queue item the latest <see cref="Cook"/> created for <see cref="CookedPlatformId"/>.
        /// </summary>
        string CookedEnvironmentId;

        /// <summary>
        /// Initializes the Play cook runner.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root.</param>
        /// <param name="sceneCatalogService">Project scene catalog.</param>
        /// <param name="platformConfigResolver">Resolves the persisted build settings of one platform.</param>
        /// <param name="selectionModelResolver">Resolves the builder-provided selection model of one platform.</param>
        /// <param name="executorFactory">Creates the platform build executor for one platform.</param>
        /// <param name="currentScenePathProvider">Supplies the saved path of the scene open in the editor.</param>
        /// <exception cref="ArgumentException">Thrown when the project root is blank.</exception>
        /// <exception cref="ArgumentNullException">Thrown when any collaborator is null.</exception>
        public EditorPlayCookRunner(
            string projectRootPath,
            EditorProjectSceneCatalogService sceneCatalogService,
            Func<string, EditorBuildPlatformConfigDocument> platformConfigResolver,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver,
            Func<string, IEditorPlayAssetCookExecutor> executorFactory,
            Func<string> currentScenePathProvider) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            SceneCatalogService = sceneCatalogService ?? throw new ArgumentNullException(nameof(sceneCatalogService));
            PlatformConfigResolver = platformConfigResolver ?? throw new ArgumentNullException(nameof(platformConfigResolver));
            SelectionModelResolver = selectionModelResolver ?? throw new ArgumentNullException(nameof(selectionModelResolver));
            ExecutorFactory = executorFactory ?? throw new ArgumentNullException(nameof(executorFactory));
            CurrentScenePathProvider = currentScenePathProvider ?? throw new ArgumentNullException(nameof(currentScenePathProvider));
        }

        /// <summary>
        /// Cooks the open scene's content for the platform and reports where the player should read it from. The
        /// environment of the cooked queue item is remembered, so the Play script stage that follows compiles with it
        /// without loading the build configuration again.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content processors drive the cook.</param>
        /// <returns>Cook outcome with the staged content root and startup scene on success.</returns>
        public EditorPlayCookResult Cook(string platformId) {
            CookedPlatformId = null;
            CookedEnvironmentId = null;
            EditorBuildQueueItemDocument queueItem = CreateQueueItem(platformId);
            CookedPlatformId = platformId;
            CookedEnvironmentId = queueItem.SelectedEnvironmentId;
            IEditorPlayAssetCookExecutor executor = ExecutorFactory(platformId);
            if (executor == null) {
                throw new InvalidOperationException($"No platform build executor could be created for '{platformId}'.");
            }

            return executor.ExecuteAssetsOnly(queueItem);
        }

        /// <summary>
        /// Resolves the environment the Play cook uses for the platform, so the Play script build compiles with the same
        /// environment symbols as the cooked content. Play cooks before it builds scripts, so when the latest cook was
        /// for this platform its queue item's environment is returned and the build configuration is not loaded again.
        /// Otherwise the queue item's environment rule is applied to the platform's build settings directly, which
        /// neither creates a queue item nor changes the settings.
        /// </summary>
        /// <param name="platformId">Platform whose Play environment is resolved.</param>
        /// <returns>Environment id the Play queue item of the platform selects.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the platform has no build settings or selection model.</exception>
        public string ResolveEnvironmentId(string platformId) {
            if (CookedPlatformId != null && string.Equals(CookedPlatformId, platformId, StringComparison.Ordinal)) {
                return CookedEnvironmentId;
            }

            return EditorBuildQueueItemDocument.ResolveEnvironmentId(ResolvePlatformConfig(platformId), ResolveSelectionModel(platformId));
        }

        /// <summary>
        /// Resolves the directory the Play queue item names as its build output.
        /// </summary>
        /// <param name="platformId">Platform whose Play output directory is resolved.</param>
        /// <returns>Absolute <c>&lt;project&gt;/cache/play/&lt;platformId&gt;/output</c> path.</returns>
        public string ResolveOutputDirectoryPath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(ProjectRootPath, CacheFolderName, PlayFolderName, platformId, OutputFolderName);
        }

        /// <summary>
        /// Snapshots the platform's build settings into a queue item and narrows its scenes to the open scene.
        /// </summary>
        /// <param name="platformId">Platform whose build settings are captured.</param>
        /// <returns>Queue item that selects only the scene open in the editor.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the platform has no build settings or selection model, or the open scene is not a project scene.</exception>
        public EditorBuildQueueItemDocument CreateQueueItem(string platformId) {
            EditorBuildPlatformConfigDocument platformConfig = ResolvePlatformConfig(platformId);
            EditorPlatformBuildSelectionModel selectionModel = ResolveSelectionModel(platformId);
            string currentSceneId = SceneCatalogService.ResolveSceneId(CurrentScenePathProvider());
            if (string.IsNullOrWhiteSpace(currentSceneId)) {
                throw new InvalidOperationException("The open scene is not saved inside the project's assets folder, so it cannot be played.");
            }

            EditorBuildQueueItemDocument queueItem = EditorBuildQueueItemDocument.Create(
                SceneCatalogService,
                platformConfig,
                selectionModel,
                ResolveOutputDirectoryPath(platformId));
            queueItem.SelectedSceneIds = new List<string> { currentSceneId };
            queueItem.SelectedSceneReferences = SceneCatalogService.CreateSceneReferences(queueItem.SelectedSceneIds);
            return queueItem;
        }

        /// <summary>
        /// Resolves the persisted build settings of one platform.
        /// </summary>
        /// <param name="platformId">Platform whose build settings are resolved.</param>
        /// <returns>Build settings of the platform.</returns>
        /// <exception cref="ArgumentException">Thrown when the platform id is blank.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the platform has no build settings.</exception>
        EditorBuildPlatformConfigDocument ResolvePlatformConfig(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            EditorBuildPlatformConfigDocument platformConfig = PlatformConfigResolver(platformId);
            if (platformConfig == null) {
                throw new InvalidOperationException($"No build settings exist for platform '{platformId}'.");
            }

            return platformConfig;
        }

        /// <summary>
        /// Resolves the builder-provided selection model of one platform.
        /// </summary>
        /// <param name="platformId">Platform whose selection model is resolved.</param>
        /// <returns>Selection model of the platform builder.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the platform exposes no selection model.</exception>
        EditorPlatformBuildSelectionModel ResolveSelectionModel(string platformId) {
            EditorPlatformBuildSelectionModel selectionModel = SelectionModelResolver(platformId);
            if (selectionModel == null) {
                throw new InvalidOperationException($"Platform '{platformId}' exposes no builder selection model.");
            }

            return selectionModel;
        }
    }
}
