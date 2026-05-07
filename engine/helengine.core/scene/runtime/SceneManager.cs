namespace helengine {
    /// <summary>
    /// Tracks loaded built scenes and raises lifecycle events without owning scene memory teardown.
    /// </summary>
    public sealed class SceneManager {
        /// <summary>
        /// Catalog used to resolve built scenes by stable identifier.
        /// </summary>
        readonly RuntimeSceneCatalog SceneCatalog;

        /// <summary>
        /// Runtime content manager used to deserialize cooked scene payloads.
        /// </summary>
        readonly ContentManager ContentManager;

        /// <summary>
        /// Runtime scene loader used to materialize scene assets into entities.
        /// </summary>
        readonly RuntimeSceneLoadService SceneLoadService;

        /// <summary>
        /// Loaded scene records preserved in load order.
        /// </summary>
        readonly List<LoadedSceneRecord> LoadedSceneRecords;

        /// <summary>
        /// Loaded scene records keyed by stable scene identifier.
        /// </summary>
        readonly Dictionary<string, LoadedSceneRecord> LoadedSceneRecordsById;

        /// <summary>
        /// Initializes one runtime scene manager.
        /// </summary>
        /// <param name="sceneCatalog">Catalog used to resolve built scenes by stable identifier.</param>
        /// <param name="contentManager">Runtime content manager used to read cooked scene payloads.</param>
        /// <param name="sceneLoadService">Runtime service used to materialize scene entities.</param>
        public SceneManager(RuntimeSceneCatalog sceneCatalog, ContentManager contentManager, RuntimeSceneLoadService sceneLoadService) {
            SceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
            ContentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            LoadedSceneRecords = new List<LoadedSceneRecord>();
            LoadedSceneRecordsById = new Dictionary<string, LoadedSceneRecord>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Raised before one built scene payload is loaded.
        /// </summary>
        public event EventHandler<SceneLoadingEventArgs> SceneLoading;

        /// <summary>
        /// Raised after one built scene payload has been loaded and tracked.
        /// </summary>
        public event EventHandler<SceneLoadedEventArgs> SceneLoaded;

        /// <summary>
        /// Raised before one tracked scene record is removed so the player can destroy its memory.
        /// </summary>
        public event EventHandler<SceneUnloadingEventArgs> SceneUnloading;

        /// <summary>
        /// Raised after one tracked scene record has been removed.
        /// </summary>
        public event EventHandler<SceneUnloadedEventArgs> SceneUnloaded;

        /// <summary>
        /// Gets the loaded scene records in load order.
        /// </summary>
        public IReadOnlyList<LoadedSceneRecord> LoadedScenes => LoadedSceneRecords;

        /// <summary>
        /// Loads one built scene using the requested runtime load mode.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load behavior to apply.</param>
        public void LoadScene(string sceneId, SceneLoadMode loadMode) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (!SceneCatalog.TryGetEntry(sceneId, out RuntimeSceneCatalogEntry entry)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' was not found in the build scene catalog.");
            }
            if (loadMode == SceneLoadMode.Additive && LoadedSceneRecordsById.ContainsKey(sceneId)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is already loaded.");
            }

            if (loadMode == SceneLoadMode.Single) {
                UnloadAllScenes();
            }

            SceneLoading?.Invoke(this, new SceneLoadingEventArgs(entry.SceneId, entry.CookedRelativePath));
            SceneAsset sceneAsset = ContentManager.Load<SceneAsset>(entry.CookedRelativePath, RuntimeContentProcessorIds.SceneAsset);
            IReadOnlyList<Entity> rootEntities = SceneLoadService.Load(sceneAsset);
            LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(entry.SceneId, entry.CookedRelativePath, rootEntities);
            LoadedSceneRecords.Add(loadedSceneRecord);
            LoadedSceneRecordsById.Add(loadedSceneRecord.SceneId, loadedSceneRecord);
            SceneLoaded?.Invoke(this, new SceneLoadedEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath,
                loadedSceneRecord.RootEntities));
        }

        /// <summary>
        /// Unloads one currently tracked scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to unload.</param>
        public void UnloadScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (!LoadedSceneRecordsById.TryGetValue(sceneId, out LoadedSceneRecord loadedSceneRecord)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is not currently loaded.");
            }

            SceneUnloading?.Invoke(this, new SceneUnloadingEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath,
                loadedSceneRecord.RootEntities));
            LoadedSceneRecordsById.Remove(loadedSceneRecord.SceneId);
            LoadedSceneRecords.Remove(loadedSceneRecord);
            SceneUnloaded?.Invoke(this, new SceneUnloadedEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath));
        }

        /// <summary>
        /// Returns whether one built scene is currently tracked as loaded.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to test.</param>
        /// <returns>True when the scene is currently tracked as loaded.</returns>
        public bool IsSceneLoaded(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return LoadedSceneRecordsById.ContainsKey(sceneId);
        }

        /// <summary>
        /// Attempts to resolve one currently tracked loaded-scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to resolve.</param>
        /// <param name="loadedSceneRecord">Tracked loaded-scene record when one exists.</param>
        /// <returns>True when the scene is currently tracked as loaded.</returns>
        public bool TryGetLoadedScene(string sceneId, out LoadedSceneRecord loadedSceneRecord) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return LoadedSceneRecordsById.TryGetValue(sceneId, out loadedSceneRecord);
        }

        /// <summary>
        /// Unloads every currently tracked scene record.
        /// </summary>
        void UnloadAllScenes() {
            while (LoadedSceneRecords.Count > 0) {
                UnloadScene(LoadedSceneRecords[0].SceneId);
            }
        }
    }
}
