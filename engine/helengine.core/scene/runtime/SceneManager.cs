namespace helengine {
    /// <summary>
    /// Tracks loaded built scenes, loads packaged payloads, and tears down runtime scene entities during unload.
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
        /// Optional editor-side resolver used to map stable scene ids back to authored scene paths when no runtime scene catalog is available.
        /// </summary>
        readonly ISceneIdPathResolver ScenePathResolver;

        /// <summary>
        /// Object manager that owns live runtime entities and allows teardown of untracked startup roots.
        /// </summary>
        readonly ObjectManager ObjectManager;

        /// <summary>
        /// Loaded scene records preserved in load order.
        /// </summary>
        readonly List<LoadedSceneRecord> LoadedSceneRecords;

        /// <summary>
        /// Loaded scene records keyed by stable scene identifier.
        /// </summary>
        readonly Dictionary<string, LoadedSceneRecord> LoadedSceneRecordsById;

        /// <summary>
        /// Deferred scene operations requested during the active object-manager update sweep.
        /// </summary>
        readonly List<PendingSceneOperation> PendingOperations;

        /// <summary>
        /// Tracks active scene-owned runtime textures and how many loaded scenes still reference each instance.
        /// </summary>
        readonly Dictionary<RuntimeTexture, int> ActiveOwnedTextureReferenceCounts;

        /// <summary>
        /// Tracks active scene-owned font assets and how many loaded scenes still reference each instance.
        /// </summary>
        readonly Dictionary<FontAsset, int> ActiveOwnedFontReferenceCounts;

        /// <summary>
        /// Tracks active scene-owned runtime models and how many loaded scenes still reference each instance.
        /// </summary>
        readonly Dictionary<RuntimeModel, int> ActiveOwnedModelReferenceCounts;

        /// <summary>
        /// Tracks active scene-owned runtime materials and how many loaded scenes still reference each instance.
        /// </summary>
        readonly Dictionary<RuntimeMaterial, int> ActiveOwnedMaterialReferenceCounts;

        /// <summary>
        /// Tracks whether deferred scene operations are currently being flushed.
        /// </summary>
        bool IsFlushingPendingOperations;

        /// <summary>
        /// Initializes one runtime scene manager.
        /// </summary>
        /// <param name="sceneCatalog">Catalog used to resolve built scenes by stable identifier.</param>
        /// <param name="contentManager">Runtime content manager used to read cooked scene payloads.</param>
        /// <param name="sceneLoadService">Runtime service used to materialize scene entities.</param>
        /// <param name="objectManager">Object manager that owns live runtime entities.</param>
        /// <param name="scenePathResolver">Optional editor-side resolver that maps stable scene ids to authored scene paths.</param>
        public SceneManager(
            RuntimeSceneCatalog sceneCatalog,
            ContentManager contentManager,
            RuntimeSceneLoadService sceneLoadService,
            ObjectManager objectManager,
            ISceneIdPathResolver scenePathResolver) {
            if (sceneCatalog == null && scenePathResolver == null) {
                throw new ArgumentNullException(nameof(sceneCatalog), "A runtime scene manager requires either a scene catalog or a scene path resolver.");
            }

            SceneCatalog = sceneCatalog;
            ContentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            ScenePathResolver = scenePathResolver;
            LoadedSceneRecords = new List<LoadedSceneRecord>();
            LoadedSceneRecordsById = new Dictionary<string, LoadedSceneRecord>(StringComparer.OrdinalIgnoreCase);
            PendingOperations = new List<PendingSceneOperation>();
            ActiveOwnedTextureReferenceCounts = new Dictionary<RuntimeTexture, int>();
            ActiveOwnedFontReferenceCounts = new Dictionary<FontAsset, int>();
            ActiveOwnedModelReferenceCounts = new Dictionary<RuntimeModel, int>();
            ActiveOwnedMaterialReferenceCounts = new Dictionary<RuntimeMaterial, int>();
        }

        /// <summary>
        /// Raised before one built scene payload is loaded.
        /// </summary>
        public event Action<SceneManager, SceneLoadingEventArgs> SceneLoading;

        /// <summary>
        /// Raised after one built scene payload has been loaded and tracked.
        /// </summary>
        public event Action<SceneManager, SceneLoadedEventArgs> SceneLoaded;

        /// <summary>
        /// Raised before one tracked scene record is removed so listeners can react while its runtime entities still exist.
        /// </summary>
        public event Action<SceneManager, SceneUnloadingEventArgs> SceneUnloading;

        /// <summary>
        /// Raised after one tracked scene record has been removed.
        /// </summary>
        public event Action<SceneManager, SceneUnloadedEventArgs> SceneUnloaded;

        /// <summary>
        /// Gets the loaded scene records in load order.
        /// </summary>
        public IReadOnlyList<LoadedSceneRecord> LoadedScenes => LoadedSceneRecords;

        /// <summary>
        /// Gets the current loaded-scene-record list capacity reserved by the manager.
        /// </summary>
        public int LoadedSceneRecordCapacity => LoadedSceneRecords.Capacity;

        /// <summary>
        /// Gets the current deferred-scene-operation list capacity reserved by the manager.
        /// </summary>
        public int PendingOperationCapacity => PendingOperations.Capacity;

        /// <summary>
        /// Gets the number of scene-owned runtime textures currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedTextureReferenceCount => ActiveOwnedTextureReferenceCounts.Count;

        /// <summary>
        /// Gets the number of scene-owned font assets currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedFontReferenceCount => ActiveOwnedFontReferenceCounts.Count;

        /// <summary>
        /// Gets the number of scene-owned runtime models currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedModelReferenceCount => ActiveOwnedModelReferenceCounts.Count;

        /// <summary>
        /// Gets the number of scene-owned runtime materials currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedMaterialReferenceCount => ActiveOwnedMaterialReferenceCounts.Count;

        /// <summary>
        /// Returns the currently loaded scene ids in load order for diagnostics.
        /// </summary>
        /// <returns>Currently loaded scene ids in deterministic load order.</returns>
        public List<string> GetLoadedSceneIds() {
            List<string> sceneIds = new List<string>(LoadedSceneRecords.Count);
            for (int index = 0; index < LoadedSceneRecords.Count; index++) {
                sceneIds.Add(LoadedSceneRecords[index].SceneId);
            }

            return sceneIds;
        }

        /// <summary>
        /// Gets the most recent scene-manager transition stage recorded for runtime diagnostics.
        /// </summary>
        public string LastTraceStage { get; private set; }

        /// <summary>
        /// Gets the most recent stable scene identifier associated with the recorded transition stage.
        /// </summary>
        public string LastTraceSceneId { get; private set; }

        /// <summary>
        /// Gets the loaded-scene count captured at the recorded transition stage.
        /// </summary>
        public int LastTraceLoadedSceneCount { get; private set; }

        /// <summary>
        /// Gets the deferred-operation count captured at the recorded transition stage.
        /// </summary>
        public int LastTracePendingOperationCount { get; private set; }

        /// <summary>
        /// Loads one built scene using the requested runtime load mode.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load behavior to apply.</param>
        public void LoadScene(string sceneId, SceneLoadMode loadMode) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            RecordTraceState("LoadSceneRequest", sceneId);
            if (ObjectManager.IsUpdateLoopActive && !IsFlushingPendingOperations) {
                PendingOperations.Add(PendingSceneOperation.CreateLoad(sceneId, loadMode));
                RecordTraceState("LoadSceneDeferred", sceneId);
                return;
            }

            LoadSceneImmediate(sceneId, loadMode);
        }

        /// <summary>
        /// Flushes any runtime scene operations that were deferred during the active object-manager update sweep.
        /// </summary>
        public void FlushPendingOperations() {
            if (PendingOperations.Count == 0) {
                return;
            }

            RecordTraceState("FlushPendingOperationsBegin", string.Empty);
            IsFlushingPendingOperations = true;
            try {
                while (PendingOperations.Count > 0) {
                    PendingSceneOperation operation = PendingOperations[0];
                    PendingOperations.RemoveAt(0);
                    RecordTraceState("FlushPendingOperationsOperation", operation.SceneId);
                    if (operation.OperationKind == PendingSceneOperationKind.Load) {
                        LoadSceneImmediate(operation.SceneId, operation.LoadMode);
                    } else {
                        UnloadSceneImmediate(operation.SceneId);
                    }
                }
            } finally {
                IsFlushingPendingOperations = false;
            }

            RecordTraceState("FlushPendingOperationsEnd", string.Empty);
        }

        /// <summary>
        /// Loads one built scene immediately.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load behavior to apply.</param>
        void LoadSceneImmediate(string sceneId, SceneLoadMode loadMode) {
            RecordTraceState("LoadSceneImmediateBegin", sceneId);
            string sceneContentPath = ResolveSceneContentPath(sceneId);
            if (loadMode == SceneLoadMode.Additive && LoadedSceneRecordsById.ContainsKey(sceneId)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is already loaded.");
            }

            if (loadMode == SceneLoadMode.Single) {
                if (LoadedSceneRecords.Count == 0) {
                    RecordTraceState("LoadSceneImmediateDisposeUntrackedRoots", sceneId);
                    DisposeUntrackedRootEntities();
                } else {
                    RecordTraceState("LoadSceneImmediateUnloadAllScenes", sceneId);
                    UnloadAllScenes();
                }

                RecordTraceState("LoadSceneImmediateFlushReleasedTextures", sceneId);
                FlushReleasedTextures();
            }

            SceneLoading?.Invoke(this, new SceneLoadingEventArgs(sceneId, sceneContentPath));
            RecordTraceState("LoadSceneImmediateBeforeContentLoad", sceneId);
            SceneAsset sceneAsset = ContentManager.Load<SceneAsset>(sceneContentPath, RuntimeContentProcessorIds.SceneAsset);
            RecordTraceState("LoadSceneImmediateBeforeSceneLoadServiceLoad", sceneId);
            RuntimeSceneLoadResult loadResult = SceneLoadService.LoadTracked(sceneAsset);
            RecordTraceState("LoadSceneImmediateAfterSceneLoadServiceLoad", sceneId);
            LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(sceneId, sceneContentPath, loadResult.RootEntities, loadResult.OwnedAssets);
            RecordTraceState("LoadSceneImmediateBeforeLoadedSceneRecordTrack", sceneId);
            LoadedSceneRecords.Add(loadedSceneRecord);
            LoadedSceneRecordsById.Add(loadedSceneRecord.SceneId, loadedSceneRecord);
            RegisterOwnedAssets(loadedSceneRecord.OwnedAssets);
            SceneLoaded?.Invoke(this, new SceneLoadedEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath,
                loadedSceneRecord.RootEntities));
            RecordTraceState("LoadSceneImmediateEnd", sceneId);
        }

        /// <summary>
        /// Unloads one currently tracked scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to unload.</param>
        public void UnloadScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (ObjectManager.IsUpdateLoopActive && !IsFlushingPendingOperations) {
                PendingOperations.Add(PendingSceneOperation.CreateUnload(sceneId));
                return;
            }

            UnloadSceneImmediate(sceneId);
        }

        /// <summary>
        /// Unloads one currently tracked scene record immediately.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to unload.</param>
        void UnloadSceneImmediate(string sceneId) {
            RecordTraceState("UnloadSceneImmediateBegin", sceneId);
            if (!LoadedSceneRecordsById.TryGetValue(sceneId, out LoadedSceneRecord loadedSceneRecord)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is not currently loaded.");
            }

            SceneUnloading?.Invoke(this, new SceneUnloadingEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath,
                loadedSceneRecord.RootEntities));
            RecordTraceState("UnloadSceneImmediateBeforeDisposeSceneRoots", loadedSceneRecord.SceneId);
            DisposeSceneRoots(loadedSceneRecord.RootEntities);
            ReleaseOwnedAssets(loadedSceneRecord.OwnedAssets);
            LoadedSceneRecordsById.Remove(loadedSceneRecord.SceneId);
            LoadedSceneRecords.Remove(loadedSceneRecord);
            SceneUnloaded?.Invoke(this, new SceneUnloadedEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath));
            RecordTraceState("UnloadSceneImmediateEnd", loadedSceneRecord.SceneId);
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
            RecordTraceState("UnloadAllScenesBegin", string.Empty);
            while (LoadedSceneRecords.Count > 0) {
                UnloadScene(LoadedSceneRecords[0].SceneId);
            }
            RecordTraceState("UnloadAllScenesEnd", string.Empty);
        }

        /// <summary>
        /// Resolves the content path that should be materialized for one stable scene identifier.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to resolve.</param>
        /// <returns>Content path that should be loaded for the supplied scene id.</returns>
        string ResolveSceneContentPath(string sceneId) {
            if (SceneCatalog != null && SceneCatalog.TryGetEntry(sceneId, out RuntimeSceneCatalogEntry entry)) {
                return entry.CookedRelativePath;
            } else if (ScenePathResolver != null) {
                string authoredScenePath = ScenePathResolver.ResolveScenePath(sceneId);
                if (string.IsNullOrWhiteSpace(authoredScenePath)) {
                    throw new InvalidOperationException($"Runtime scene '{sceneId}' resolved to an empty authored scene path.");
                }

                return authoredScenePath;
            }

            throw new InvalidOperationException($"Runtime scene '{sceneId}' was not found in the build scene catalog and no scene path resolver was configured.");
        }

        /// <summary>
        /// Disposes every root entity that belongs to one loaded runtime scene.
        /// </summary>
        /// <param name="rootEntities">Root entities that should be torn down.</param>
        void DisposeSceneRoots(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            for (int index = rootEntities.Count - 1; index >= 0; index--) {
                rootEntities[index].Dispose();
            }
        }

        /// <summary>
        /// Disposes live root entities that exist before any built scene has been tracked, such as startup scenes loaded directly by the host.
        /// </summary>
        void DisposeUntrackedRootEntities() {
            List<Entity> rootEntities = new List<Entity>();
            for (int index = 0; index < ObjectManager.Entities.Count; index++) {
                Entity entity = ObjectManager.Entities[index];
                if (entity.Parent == null) {
                    rootEntities.Add(entity);
                }
            }

            for (int index = rootEntities.Count - 1; index >= 0; index--) {
                rootEntities[index].Dispose();
            }
        }

        /// <summary>
        /// Registers one scene's owned runtime assets against the active scene set.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned runtime assets resolved during materialization.</param>
        void RegisterOwnedAssets(RuntimeSceneOwnedAssetSet ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            RegisterOwnedTextures(ownedAssets.OwnedTextures);
            RegisterOwnedFonts(ownedAssets.OwnedFonts);
            RegisterOwnedModels(ownedAssets.OwnedModels);
            RegisterOwnedMaterials(ownedAssets.OwnedMaterials);
        }

        /// <summary>
        /// Registers one scene's owned runtime textures against the active scene set.
        /// </summary>
        /// <param name="ownedTextures">Scene-owned runtime textures resolved during materialization.</param>
        void RegisterOwnedTextures(IReadOnlyList<RuntimeTexture> ownedTextures) {
            if (ownedTextures == null) {
                throw new ArgumentNullException(nameof(ownedTextures));
            }

            for (int assetIndex = 0; assetIndex < ownedTextures.Count; assetIndex++) {
                RuntimeTexture ownedAsset = ownedTextures[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }

                if (ActiveOwnedTextureReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    ActiveOwnedTextureReferenceCounts[ownedAsset] = existingReferenceCount + 1;
                } else {
                    ActiveOwnedTextureReferenceCounts.Add(ownedAsset, 1);
                }
            }
        }

        /// <summary>
        /// Registers one scene's owned font assets against the active scene set.
        /// </summary>
        /// <param name="ownedFonts">Scene-owned font assets resolved during materialization.</param>
        void RegisterOwnedFonts(IReadOnlyList<FontAsset> ownedFonts) {
            if (ownedFonts == null) {
                throw new ArgumentNullException(nameof(ownedFonts));
            }

            for (int assetIndex = 0; assetIndex < ownedFonts.Count; assetIndex++) {
                FontAsset ownedAsset = ownedFonts[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }

                if (ActiveOwnedFontReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    ActiveOwnedFontReferenceCounts[ownedAsset] = existingReferenceCount + 1;
                } else {
                    ActiveOwnedFontReferenceCounts.Add(ownedAsset, 1);
                }
            }
        }

        /// <summary>
        /// Registers one scene's owned runtime models against the active scene set.
        /// </summary>
        /// <param name="ownedModels">Scene-owned runtime models resolved during materialization.</param>
        void RegisterOwnedModels(IReadOnlyList<RuntimeModel> ownedModels) {
            if (ownedModels == null) {
                throw new ArgumentNullException(nameof(ownedModels));
            }

            for (int assetIndex = 0; assetIndex < ownedModels.Count; assetIndex++) {
                RuntimeModel ownedAsset = ownedModels[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }

                if (ActiveOwnedModelReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    ActiveOwnedModelReferenceCounts[ownedAsset] = existingReferenceCount + 1;
                } else {
                    ActiveOwnedModelReferenceCounts.Add(ownedAsset, 1);
                }
            }
        }

        /// <summary>
        /// Registers one scene's owned runtime materials against the active scene set.
        /// </summary>
        /// <param name="ownedMaterials">Scene-owned runtime materials resolved during materialization.</param>
        void RegisterOwnedMaterials(IReadOnlyList<RuntimeMaterial> ownedMaterials) {
            if (ownedMaterials == null) {
                throw new ArgumentNullException(nameof(ownedMaterials));
            }

            for (int assetIndex = 0; assetIndex < ownedMaterials.Count; assetIndex++) {
                RuntimeMaterial ownedAsset = ownedMaterials[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }

                if (ActiveOwnedMaterialReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    ActiveOwnedMaterialReferenceCounts[ownedAsset] = existingReferenceCount + 1;
                } else {
                    ActiveOwnedMaterialReferenceCounts.Add(ownedAsset, 1);
                }
            }
        }

        /// <summary>
        /// Releases one scene's owned runtime assets when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned runtime assets resolved during materialization.</param>
        void ReleaseOwnedAssets(RuntimeSceneOwnedAssetSet ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            ReleaseOwnedFonts(ownedAssets.OwnedFonts);
            ReleaseOwnedTextures(ownedAssets.OwnedTextures);
            ReleaseOwnedModels(ownedAssets.OwnedModels);
            ReleaseOwnedMaterials(ownedAssets.OwnedMaterials);
        }

        /// <summary>
        /// Releases one scene's owned font assets when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedFonts">Scene-owned font assets resolved during materialization.</param>
        void ReleaseOwnedFonts(IReadOnlyList<FontAsset> ownedFonts) {
            if (ownedFonts == null) {
                throw new ArgumentNullException(nameof(ownedFonts));
            }

            for (int assetIndex = 0; assetIndex < ownedFonts.Count; assetIndex++) {
                FontAsset ownedAsset = ownedFonts[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }
                if (!ActiveOwnedFontReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    throw new InvalidOperationException("Scene-owned font asset was not tracked before release.");
                }

                if (existingReferenceCount > 1) {
                    ActiveOwnedFontReferenceCounts[ownedAsset] = existingReferenceCount - 1;
                    continue;
                }

                ActiveOwnedFontReferenceCounts.Remove(ownedAsset);
                ReleaseOwnedFont(ownedAsset);
            }
        }

        /// <summary>
        /// Releases one scene's owned runtime textures when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedTextures">Scene-owned runtime textures resolved during materialization.</param>
        void ReleaseOwnedTextures(IReadOnlyList<RuntimeTexture> ownedTextures) {
            if (ownedTextures == null) {
                throw new ArgumentNullException(nameof(ownedTextures));
            }

            for (int assetIndex = 0; assetIndex < ownedTextures.Count; assetIndex++) {
                RuntimeTexture ownedAsset = ownedTextures[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }
                if (!ActiveOwnedTextureReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    throw new InvalidOperationException("Scene-owned runtime texture was not tracked before release.");
                }

                if (existingReferenceCount > 1) {
                    ActiveOwnedTextureReferenceCounts[ownedAsset] = existingReferenceCount - 1;
                    continue;
                }

                ActiveOwnedTextureReferenceCounts.Remove(ownedAsset);
                ReleaseOwnedAsset(ownedAsset);
            }
        }

        /// <summary>
        /// Releases one scene's owned runtime models when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedModels">Scene-owned runtime models resolved during materialization.</param>
        void ReleaseOwnedModels(IReadOnlyList<RuntimeModel> ownedModels) {
            if (ownedModels == null) {
                throw new ArgumentNullException(nameof(ownedModels));
            }

            for (int assetIndex = 0; assetIndex < ownedModels.Count; assetIndex++) {
                RuntimeModel ownedAsset = ownedModels[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }
                if (!ActiveOwnedModelReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    throw new InvalidOperationException("Scene-owned runtime model was not tracked before release.");
                }

                if (existingReferenceCount > 1) {
                    ActiveOwnedModelReferenceCounts[ownedAsset] = existingReferenceCount - 1;
                    continue;
                }

                ActiveOwnedModelReferenceCounts.Remove(ownedAsset);
                ReleaseOwnedModel(ownedAsset);
            }
        }

        /// <summary>
        /// Releases one scene's owned runtime materials when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedMaterials">Scene-owned runtime materials resolved during materialization.</param>
        void ReleaseOwnedMaterials(IReadOnlyList<RuntimeMaterial> ownedMaterials) {
            if (ownedMaterials == null) {
                throw new ArgumentNullException(nameof(ownedMaterials));
            }

            for (int assetIndex = 0; assetIndex < ownedMaterials.Count; assetIndex++) {
                RuntimeMaterial ownedAsset = ownedMaterials[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }
                if (!ActiveOwnedMaterialReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    throw new InvalidOperationException("Scene-owned runtime material was not tracked before release.");
                }

                if (existingReferenceCount > 1) {
                    ActiveOwnedMaterialReferenceCounts[ownedAsset] = existingReferenceCount - 1;
                    continue;
                }

                ActiveOwnedMaterialReferenceCounts.Remove(ownedAsset);
                ReleaseOwnedMaterial(ownedAsset);
            }
        }

        /// <summary>
        /// Releases one scene-owned font asset after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned font asset that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedFont(FontAsset ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (ownedAsset.IsDisposed) {
                return;
            }
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Font asset release requires an initialized 2D render manager.");
            }

            Core.Instance.RenderManager2D.ReleaseFont(ownedAsset);
        }

        /// <summary>
        /// Releases one scene-owned runtime asset through the correct runtime ownership seam.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned runtime asset that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedAsset(RuntimeTexture ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (ownedAsset.IsDisposed) {
                return;
            }
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Runtime texture release requires an initialized 2D render manager.");
            }

            Core.Instance.RenderManager2D.ReleaseTexture(ownedAsset);
            ownedAsset.Dispose();
        }

        /// <summary>
        /// Releases one scene-owned runtime model after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned runtime model that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedModel(RuntimeModel ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                throw new InvalidOperationException("Runtime model release requires an initialized 3D render manager.");
            }

            Core.Instance.RenderManager3D.ReleaseModel(ownedAsset);
        }

        /// <summary>
        /// Releases one scene-owned runtime material after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned runtime material that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedMaterial(RuntimeMaterial ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (Core.Instance == null || Core.Instance.RenderManager3D == null) {
                throw new InvalidOperationException("Runtime material release requires an initialized 3D render manager.");
            }

            Core.Instance.RenderManager3D.ReleaseMaterial(ownedAsset);
        }

        /// <summary>
        /// Flushes any renderer-owned runtime texture releases that were deferred during scene unload before the next scene begins loading.
        /// </summary>
        void FlushReleasedTextures() {
            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Deferred runtime texture release flushing requires an initialized 2D render manager.");
            }

            Core.Instance.RenderManager2D.FlushReleasedTextures();
            if (Core.Instance.RenderManager3D == null) {
                throw new InvalidOperationException("Deferred runtime asset release flushing requires an initialized 3D render manager.");
            }

            Core.Instance.RenderManager3D.FlushReleasedAssets();
        }

        /// <summary>
        /// Records one focused scene-transition diagnostic snapshot that native hosts can inspect after failures.
        /// </summary>
        /// <param name="stage">Short stage name describing the current transition boundary.</param>
        /// <param name="sceneId">Stable scene identifier associated with the current transition boundary.</param>
        void RecordTraceState(string stage, string sceneId) {
            LastTraceStage = stage;
            LastTraceSceneId = sceneId;
            LastTraceLoadedSceneCount = LoadedSceneRecords.Count;
            LastTracePendingOperationCount = PendingOperations.Count;
        }
    }
}
