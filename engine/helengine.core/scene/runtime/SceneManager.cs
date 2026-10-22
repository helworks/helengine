namespace helengine {
    /// <summary>
    /// Tracks loaded built scenes, loads packaged payloads, and tears down runtime scene entities during unload.
    /// </summary>
    public sealed class SceneManager : IDisposable {
        readonly Core OwnerCore;
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
        /// Optional diagnostics provider that receives live scene-manager transition stages.
        /// </summary>
        readonly IRuntimeSceneTransitionDiagnosticsProvider SceneTransitionDiagnosticsProvider;

        /// <summary>
        /// Optional diagnostics provider that receives live entity-disposal stages during scene teardown.
        /// </summary>
        readonly IRuntimeEntityDisposalDiagnosticsProvider EntityDisposalDiagnosticsProvider;

        /// <summary>
        /// Loaded scene records preserved in load order.
        /// </summary>
        readonly List<LoadedSceneRecord> LoadedSceneRecords;

        /// <summary>
        /// Loaded scene records keyed by stable scene identifier.
        /// </summary>
        readonly Dictionary<string, LoadedSceneRecord> LoadedSceneRecordsById;

        /// <summary>
        /// Deferred scene operations requested for the next frame-boundary commit.
        /// </summary>
        readonly List<PendingSceneOperation> PendingOperations;

        /// <summary>
        /// Tracks active scene-owned runtime textures and how many loaded scenes still reference each instance.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<RuntimeTexture> OwnedTextureTable;

        /// <summary>
        /// Tracks active scene-owned font assets and how many loaded scenes still reference each instance.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<FontAsset> OwnedFontTable;

        /// <summary>
        /// Tracks active scene-owned audio assets and how many loaded scenes still reference each instance.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<AudioAsset> OwnedAudioTable;

        /// <summary>
        /// Tracks active scene-owned runtime models and how many loaded scenes still reference each instance.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<RuntimeModel> OwnedModelTable;

        /// <summary>
        /// Tracks active scene-owned runtime materials and how many loaded scenes still reference each instance.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<RuntimeMaterial> OwnedMaterialTable;

        /// <summary>
        /// Tracks animation clips until the last referencing scene has disposed its entities.
        /// </summary>
        readonly SceneOwnedAssetReferenceTable<AnimationClipAsset> OwnedAnimationClipTable;

        /// <summary>
        /// Tracks whether deferred scene operations are currently being committed at the frame boundary.
        /// </summary>
        bool IsCommittingPendingOperations;

        /// <summary>
        /// Tracks whether an engine-owned single-scene transition is currently in progress.
        /// </summary>
        bool IsSceneTransitionActiveValue;

        /// <summary>
        /// Stores the stable identifier of the scene currently requested through the transition path.
        /// </summary>
        string TransitionTargetSceneIdValue = string.Empty;

        /// <summary>
        /// Stores the normalized progress reported by the active or most recently completed scene transition.
        /// </summary>
        float SceneTransitionProgressValue = 1f;

        /// <summary>
        /// Stores the transient packaged scene payload currently being materialized by an active transition.
        /// </summary>
        SceneAsset TransitionSceneAsset;

        /// <summary>
        /// Stores the resumable runtime load operation currently advanced by an active transition.
        /// </summary>
        RuntimeSceneLoadOperation TransitionLoadOperation;

        /// <summary>
        /// Stores the cooked content path resolved for the active transition target.
        /// </summary>
        string TransitionSceneContentPath = string.Empty;

        /// <summary>Remembers the presentation-only arm boundary so the loading screen draws once before synchronous content IO.</summary>
        bool TransitionPresentationArmed;

        /// <summary>Owned pending code acquisition, advanced before any component payload is deserialized.</summary>
        [NativeOwnedMember] SceneModuleLoadOperation TransitionModuleOperation;
        /// <summary>Records completion of code preparation for the current transition.</summary>
        bool TransitionModulesReady;
        /// <summary>Records completion of asynchronous content preloading for the current transition.</summary>
        bool TransitionContentReady;
        /// <summary>Owns pending content preloads and completed cache leases until scene unload, including dependencies reopened by live components.</summary>
        [NativeOwnedMember] Dictionary<string, IRuntimeSceneContentLoadOperation> ContentLeases;

        /// <summary>Owns the monotonic clock used to account for resumable scene preparation.</summary>
        [NativeOwnedMember]
        RuntimeSceneLoadingClock SceneLoadingClock = new RuntimeSceneLoadingClock();

        /// <summary>Stores the finite positive cooperative budget applied to each frame-boundary transition advance.</summary>
        double SceneLoadingBudgetMillisecondsValue = 8d;

        /// <summary>
        /// Gets or sets the scene-transition budget in milliseconds per frame; smaller entity/component/readiness steps yield after it expires.
        /// Synchronous IO, a single asset preparation/upload or a lifecycle callback can exceed the budget because those steps are indivisible.
        /// </summary>
        public double SceneLoadingBudgetMilliseconds {
            get { return SceneLoadingBudgetMillisecondsValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Scene loading budget must be finite and greater than zero.");
                }
                SceneLoadingBudgetMillisecondsValue = value;
            }
        }

        /// <summary>
        /// Initializes one runtime scene manager.
        /// </summary>
        /// <param name="sceneCatalog">Catalog used to resolve built scenes by stable identifier.</param>
        /// <param name="contentManager">Runtime content manager used to read cooked scene payloads.</param>
        /// <param name="sceneLoadService">Runtime service used to materialize scene entities.</param>
        /// <param name="objectManager">Object manager that owns live runtime entities.</param>
        /// <param name="scenePathResolver">Optional editor-side resolver that maps stable scene ids to authored scene paths.</param>
        /// <param name="sceneTransitionDiagnosticsProvider">Optional diagnostics provider that receives live scene-manager transition stages.</param>
        /// <param name="entityDisposalDiagnosticsProvider">Optional diagnostics provider that receives live entity-disposal stages during scene teardown.</param>
        public SceneManager(
            Core ownerCore,
            RuntimeSceneCatalog sceneCatalog,
            ContentManager contentManager,
            RuntimeSceneLoadService sceneLoadService,
            ObjectManager objectManager,
            ISceneIdPathResolver scenePathResolver,
            IRuntimeSceneTransitionDiagnosticsProvider sceneTransitionDiagnosticsProvider,
            IRuntimeEntityDisposalDiagnosticsProvider entityDisposalDiagnosticsProvider) {
            OwnerCore = ownerCore ?? throw new ArgumentNullException(nameof(ownerCore));
            if (sceneCatalog == null && scenePathResolver == null) {
                throw new InvalidOperationException("A runtime scene manager requires either a scene catalog or a scene path resolver.");
            }

            SceneCatalog = sceneCatalog;
            ContentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            if (!ReferenceEquals(SceneLoadService.OwningCore, OwnerCore)) {
                throw new InvalidOperationException("Scene manager and runtime scene load service must share the same owning core.");
            }
            if (!ReferenceEquals(ObjectManager.OwnerCore, OwnerCore)) {
                throw new InvalidOperationException("Scene manager and object manager must share the same owning core.");
            }
            ScenePathResolver = scenePathResolver;
            SceneTransitionDiagnosticsProvider = sceneTransitionDiagnosticsProvider;
            EntityDisposalDiagnosticsProvider = entityDisposalDiagnosticsProvider;
            LoadedSceneRecords = new List<LoadedSceneRecord>();
            LoadedSceneRecordsById = new Dictionary<string, LoadedSceneRecord>(StringComparer.OrdinalIgnoreCase);
            PendingOperations = new List<PendingSceneOperation>();
            ContentLeases = new Dictionary<string, IRuntimeSceneContentLoadOperation>(StringComparer.OrdinalIgnoreCase);
            OwnedTextureTable = new SceneOwnedAssetReferenceTable<RuntimeTexture>("runtime texture", ReleaseOwnedAsset);
            OwnedFontTable = new SceneOwnedAssetReferenceTable<FontAsset>("font asset", ReleaseOwnedFont);
            OwnedAudioTable = new SceneOwnedAssetReferenceTable<AudioAsset>("audio asset", ReleaseOwnedAudioAsset);
            OwnedModelTable = new SceneOwnedAssetReferenceTable<RuntimeModel>("runtime model", ReleaseOwnedModel);
            OwnedMaterialTable = new SceneOwnedAssetReferenceTable<RuntimeMaterial>("runtime material", ReleaseOwnedMaterial);
            OwnedAnimationClipTable = new SceneOwnedAssetReferenceTable<AnimationClipAsset>("animation clip", ReleaseOwnedAnimationClip);
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
        /// Gets whether an engine-owned single-scene transition is active.
        /// </summary>
        public bool IsSceneTransitionActive => IsSceneTransitionActiveValue;

        /// <summary>Gets whether direct scene load or unload requests await a frame boundary, allowing native hosts to fence GPU resources only before scene mutation.</summary>
        public bool HasPendingSceneOperations => PendingOperations.Count != 0;

        /// <summary>
        /// Gets the stable identifier of the target scene requested through the transition path.
        /// </summary>
        public string TransitionTargetSceneId => TransitionTargetSceneIdValue;

        /// <summary>
        /// Gets the normalized progress of the active or most recently completed transition.
        /// </summary>
        public float SceneTransitionProgress => SceneTransitionProgressValue;

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
        public int ActiveOwnedTextureReferenceCount => OwnedTextureTable.Count;

        /// <summary>
        /// Gets the number of scene-owned font assets currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedFontReferenceCount => OwnedFontTable.Count;

        /// <summary>
        /// Gets the number of scene-owned audio assets currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedAudioReferenceCount => OwnedAudioTable.Count;

        /// <summary>
        /// Gets the number of scene-owned runtime models currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedModelReferenceCount => OwnedModelTable.Count;

        /// <summary>
        /// Gets the number of scene-owned runtime materials currently tracked across all loaded scenes.
        /// </summary>
        public int ActiveOwnedMaterialReferenceCount => OwnedMaterialTable.Count;

        /// <summary>
        /// Gets the number of distinct animation clips retained by loaded scenes.
        /// </summary>
        public int ActiveOwnedAnimationClipReferenceCount => OwnedAnimationClipTable.Count;

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
        /// Tracks one externally materialized scene so editor-hosted authored scenes appear in runtime scene queries.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that should be tracked as loaded.</param>
        /// <param name="rootEntities">Live root entities that currently represent the loaded scene.</param>
        /// <param name="dontUnload">True when the externally tracked scene should survive single-scene runtime transitions.</param>
        public void TrackExternallyLoadedScene(string sceneId, IReadOnlyList<Entity> rootEntities, bool dontUnload) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            if (LoadedSceneRecordsById.ContainsKey(sceneId)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is already tracked as loaded.");
            }

            string sceneContentPath = ResolveSceneContentPath(sceneId);
            LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(
                sceneId,
                sceneContentPath,
                rootEntities,
                CreateEmptyOwnedAssetSet(),
                dontUnload);
            RecordTraceState("TrackExternallyLoadedSceneBeforeTrack", sceneId);
            TrackLoadedSceneRecord(loadedSceneRecord);
            RecordTraceState("TrackExternallyLoadedSceneAfterTrack", sceneId);
        }

        /// <summary>
        /// Transfers one loaded scene record to this manager's explicit unload lifecycle and stores borrowing indices for lookup and load order.
        /// </summary>
        /// <param name="loadedSceneRecord">New record whose native lifetime becomes owned by this scene manager.</param>
        /// <returns>The tracked record borrowed from this manager until the corresponding unload or untrack operation.</returns>
        [NativeBorrowedReturn]
        LoadedSceneRecord TrackLoadedSceneRecord([NativeTakesOwnership] LoadedSceneRecord loadedSceneRecord) {
            if (loadedSceneRecord == null) {
                throw new ArgumentNullException(nameof(loadedSceneRecord));
            }

            RetainLoadedSceneRecordReferences(loadedSceneRecord);
            return LoadedSceneRecords[LoadedSceneRecords.Count - 1];
        }

        /// <summary>
        /// Keeps two lookup aliases to one manually owned record without allowing either native index to destroy it.
        /// Unload removes these aliases only after entity and asset teardown, then deletes the record once.
        /// </summary>
        /// <param name="loadedSceneRecord">Record whose lifetime remains owned by this scene manager.</param>
        void RetainLoadedSceneRecordReferences([NativeRetainsBorrow] LoadedSceneRecord loadedSceneRecord) {
            LoadedSceneRecordsById.Add(loadedSceneRecord.SceneId, loadedSceneRecord);
            LoadedSceneRecords.Add(loadedSceneRecord);
        }

        /// <summary>
        /// Stops tracking one externally materialized scene without disposing its entities or releasing externally owned assets.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that should no longer be tracked as loaded.</param>
        /// <returns>True when one tracked external scene record was removed; otherwise false.</returns>
        public bool TryUntrackExternallyLoadedScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (!LoadedSceneRecordsById.TryGetValue(sceneId, out LoadedSceneRecord loadedSceneRecord)) {
                return false;
            }
            int loadedSceneRecordIndex = LoadedSceneRecords.IndexOf(loadedSceneRecord);
            if (loadedSceneRecordIndex < 0) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is indexed without a load-order record.");
            }

            RecordTraceState("TryUntrackExternallyLoadedSceneBeforeRemove", sceneId);
            LoadedSceneRecordsById.Remove(loadedSceneRecord.SceneId);
            NativeOwnership.DisposeAndDelete(loadedSceneRecord.OwnedAssets);
            NativeOwnership.Delete(LoadedSceneRecords[loadedSceneRecordIndex]);
            LoadedSceneRecords.RemoveAt(loadedSceneRecordIndex);
            RecordTraceState("TryUntrackExternallyLoadedSceneAfterRemove", sceneId);
            return true;
        }

        /// <summary>
        /// Gets the most recent scene-manager transition stage recorded for runtime diagnostics.
        /// </summary>
        public string LastTraceStage { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the most recent stable scene identifier associated with the recorded transition stage.
        /// </summary>
        public string LastTraceSceneId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the loaded-scene count captured at the recorded transition stage.
        /// </summary>
        public int LastTraceLoadedSceneCount { get; private set; }

        /// <summary>
        /// Gets the deferred-operation count captured at the recorded transition stage.
        /// </summary>
        public int LastTracePendingOperationCount { get; private set; }

        /// <summary>
        /// Gets the monotonically increasing transition serial captured at the recorded transition stage.
        /// </summary>
        public int LastTraceSerial { get; private set; }

        /// <summary>
        /// Loads one built scene using the requested runtime load mode.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load behavior to apply.</param>
        public void LoadScene(string sceneId, SceneLoadMode loadMode) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (loadMode == SceneLoadMode.Single) {
                DiscardPendingLoadOperationsForSingleLoad();
            }

            RecordTraceState("LoadSceneRequest", sceneId);
            PendingOperations.Add(PendingSceneOperation.CreateLoad(sceneId, loadMode));
            RecordTraceState("LoadSceneDeferred", sceneId);
        }

        /// <summary>
        /// Requests one normal single-scene transition and exposes its loading state to persistent presentation scenes while preserving an already active transition.
        /// </summary>
        /// <param name="sceneId">Stable identifier of the scene that should replace non-persistent loaded scenes.</param>
        public void RequestSceneTransition(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            } else if (IsSceneTransitionActiveValue) {
                RecordTraceState("RequestSceneTransitionIgnored", sceneId);
                return;
            }

            RecordTraceState("RequestSceneTransitionAccepted", sceneId);
            IsSceneTransitionActiveValue = true;
            TransitionPresentationArmed = false;
            TransitionTargetSceneIdValue = sceneId;
            SceneTransitionProgressValue = 0f;
        }

        /// <summary>
        /// Removes queued scene-load operations superseded by a newly requested single-scene transition while preserving explicit unload operations.
        /// </summary>
        void DiscardPendingLoadOperationsForSingleLoad() {
            for (int operationIndex = PendingOperations.Count - 1; operationIndex >= 0; operationIndex--) {
                if (PendingOperations[operationIndex].OperationKind != PendingSceneOperationKind.Load) {
                    continue;
                }

                NativeOwnership.Delete(PendingOperations[operationIndex]);
                PendingOperations.RemoveAt(operationIndex);
            }
        }

        /// <summary>
        /// Commits any runtime scene operations that were deferred for the frame boundary.
        /// </summary>
        /// <returns>Number of scene operations advanced or committed at the safe point.</returns>
        public int CommitPendingOperationsAtFrameBoundary() {
            if (PendingOperations.Count == 0 && !IsSceneTransitionActiveValue) {
                return 0;
            }

            if (IsSceneTransitionActiveValue) {
                AdvanceSceneTransition();
                return 1;
            }

            RecordTraceState("CommitPendingOperationsAtFrameBoundaryBegin", string.Empty);
            int operationCountToCommit = PendingOperations.Count;
            bool shouldFlushReleasedAssetsAtFrameBoundary = false;
            IsCommittingPendingOperations = true;
            try {
                for (int operationIndex = 0; operationIndex < operationCountToCommit; operationIndex++) {
                    string operationSceneId = PendingOperations[0].SceneId;
                    PendingSceneOperationKind operationKind = PendingOperations[0].OperationKind;
                    SceneLoadMode operationLoadMode = PendingOperations[0].LoadMode;
                    NativeOwnership.Delete(PendingOperations[0]);
                    PendingOperations.RemoveAt(0);
                    RecordTraceState("CommitPendingOperationsAtFrameBoundaryOperation", operationSceneId);
                    if (operationKind == PendingSceneOperationKind.Load && shouldFlushReleasedAssetsAtFrameBoundary) {
                        FlushReleasedAssets();
                        shouldFlushReleasedAssetsAtFrameBoundary = false;
                    }
                    if (operationKind == PendingSceneOperationKind.Load) {
                        LoadSceneImmediate(operationSceneId, operationLoadMode);
                        if (IsSceneTransitionActiveValue
                            && operationLoadMode == SceneLoadMode.Single
                            && string.Equals(operationSceneId, TransitionTargetSceneIdValue, StringComparison.OrdinalIgnoreCase)) {
                            SceneTransitionProgressValue = 1f;
                            IsSceneTransitionActiveValue = false;
                        }
                    } else {
                        UnloadSceneImmediate(operationSceneId);
                        shouldFlushReleasedAssetsAtFrameBoundary = true;
                    }
                }
            } finally {
                IsCommittingPendingOperations = false;
            }

            if (shouldFlushReleasedAssetsAtFrameBoundary) {
                FlushReleasedAssets();
            }

            RecordTraceState("CommitPendingOperationsAtFrameBoundaryEnd", string.Empty);
            return operationCountToCommit;
        }

        /// <summary>
        /// Loads one built scene immediately.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load behavior to apply.</param>
        void LoadSceneImmediate(string sceneId, SceneLoadMode loadMode) {
            bool succeeded = false;
            try {
                LoadPreparedSceneImmediate(sceneId, loadMode);
                succeeded = true;
            } finally {
                ReleaseSceneContentPreparation(sceneId);
                if (!succeeded && !LoadedSceneRecordsById.ContainsKey(sceneId)) { ReleaseScenePreparation(sceneId); }
            }
        }

        /// <summary>Loads a scene synchronously only when optional native preparations can complete immediately.</summary>
        /// <param name="sceneId">Stable scene identity.</param>
        /// <param name="loadMode">Single or additive lifetime behavior.</param>
        void LoadPreparedSceneImmediate(string sceneId, SceneLoadMode loadMode) {
            RecordTraceState("LoadSceneImmediateBegin", sceneId);
            RecordTraceState("LoadSceneImmediateBeforeResolveSceneContentPath", sceneId);
            string sceneContentPath = ResolveSceneContentPath(sceneId);
            RecordTraceState("LoadSceneImmediateAfterResolveSceneContentPath", sceneId);
            RecordTraceState("LoadSceneImmediateBeforeLoadedSceneRecordLookup", sceneId);
            if (LoadedSceneRecordsById.ContainsKey(sceneId)) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is already loaded.");
            }
            RecordTraceState("LoadSceneImmediateAfterLoadedSceneRecordLookup", sceneId);

            if (loadMode == SceneLoadMode.Single) {
                if (LoadedSceneRecords.Count == 0) {
                    RecordTraceState("LoadSceneImmediateDisposeUntrackedRoots", sceneId);
                    DisposeUntrackedRootEntities();
                } else {
                    RecordTraceState("LoadSceneImmediateUnloadSingleModeScenes", sceneId);
                    UnloadScenesForSingleLoad();
                }

                RecordTraceState("LoadSceneImmediateFlushReleasedTextures", sceneId);
                FlushReleasedAssets();
                RecordTraceState("LoadSceneImmediateResetPhysicsTiming", sceneId);
                ResetPhysicsTimingForSingleLoad();
            }

            SceneLoadingEventArgs sceneLoadingEventArgs = new SceneLoadingEventArgs(sceneId, sceneContentPath);
            try {
                DispatchSceneLoading(sceneLoadingEventArgs);
            } finally {
                NativeOwnership.Delete(sceneLoadingEventArgs);
            }
            PrepareSceneSynchronously(sceneId);
            RecordTraceState("LoadSceneImmediateBeforeContentLoad", sceneId);
            SceneAsset sceneAsset = ContentManager.Load<SceneAsset>(sceneContentPath, RuntimeContentProcessorIds.SceneAsset);
            try {
                bool dontUnload = sceneAsset.SceneSettings != null && sceneAsset.SceneSettings.DontUnload;
                RecordTraceState("LoadSceneImmediateBeforeSceneLoadServiceLoad", sceneId);
                RuntimeSceneLoadResult loadResult = SceneLoadService.LoadTracked(sceneAsset);
                try {
                    RecordTraceState("LoadSceneImmediateAfterSceneLoadServiceLoad", sceneId);
                    LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(sceneId, sceneContentPath, loadResult.RootEntities, loadResult.OwnedAssets, dontUnload);
                    RecordTraceState("LoadSceneImmediateBeforeLoadedSceneRecordTrack", sceneId);
                    LoadedSceneRecord trackedSceneRecord = TrackLoadedSceneRecord(loadedSceneRecord);
                    RecordTraceState("LoadSceneImmediateAfterLoadedSceneRecordListAdd", sceneId);
                    RecordTraceState("LoadSceneImmediateAfterLoadedSceneRecordDictionaryAdd", sceneId);
                    RegisterOwnedAssets(trackedSceneRecord.OwnedAssets, sceneId);
                    RecordTraceState("LoadSceneImmediateBeforeSceneLoadedEvent", sceneId);
                    SceneLoadedEventArgs sceneLoadedEventArgs = new SceneLoadedEventArgs(
                        trackedSceneRecord.SceneId,
                        trackedSceneRecord.CookedRelativePath,
                        trackedSceneRecord.RootEntities);
                    try {
                        DispatchSceneLoaded(sceneLoadedEventArgs);
                        OwnerCore.ReportSceneTransitionStage("AfterSceneLoadedEventDispatch");
                    } finally {
                        OwnerCore.ReportSceneTransitionStage("BeforeSceneLoadedEventArgsRelease");
                        NativeOwnership.Delete(sceneLoadedEventArgs);
                        OwnerCore.ReportSceneTransitionStage("AfterSceneLoadedEventArgsRelease");
                    }
                    RecordTraceState("LoadSceneImmediateAfterSceneLoadedEvent", sceneId);
                    RecordTraceState("LoadSceneImmediateEnd", sceneId);
                } finally {
                    NativeOwnership.Delete(loadResult);
                }
            } finally {
                ReleaseTransientSceneAsset(sceneAsset);
            }
        }

        /// <summary>
        /// Unloads one currently tracked scene record.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to unload.</param>
        public void UnloadScene(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            PendingOperations.Add(PendingSceneOperation.CreateUnload(sceneId));
            RecordTraceState("UnloadSceneDeferred", sceneId);
        }

        /// <summary>
        /// Advances the active engine-owned scene transition by its next observable loading stage.
        /// </summary>
        void AdvanceSceneTransition() {
            bool succeeded = false;
            try {
                AdvancePreparedSceneTransition();
                succeeded = true;
            } finally {
                if (!succeeded) {
                    NativeOwnership.DisposeAndRelease(ref TransitionLoadOperation);
                    if (TransitionSceneAsset != null) { ReleaseTransientSceneAsset(TransitionSceneAsset); TransitionSceneAsset = null; }
                    NativeOwnership.DisposeAndRelease(ref TransitionModuleOperation);
                    if (!LoadedSceneRecordsById.ContainsKey(TransitionTargetSceneIdValue)) { ReleaseScenePreparation(TransitionTargetSceneIdValue); }
                }
            }
        }

        /// <summary>Advances code, asynchronous content and entity preparation at separate observable frame boundaries.</summary>
        void AdvancePreparedSceneTransition() {
            if (TransitionLoadOperation == null && !TransitionPresentationArmed) {
                TransitionSceneContentPath = ResolveSceneContentPath(TransitionTargetSceneIdValue);
                UnloadScenesForSingleLoad();
                FlushReleasedAssets();
                ResetPhysicsTimingForSingleLoad();
                SceneLoadingEventArgs sceneLoadingEventArgs = new SceneLoadingEventArgs(TransitionTargetSceneIdValue, TransitionSceneContentPath);
                try {
                    DispatchSceneLoading(sceneLoadingEventArgs);
                } finally {
                    NativeOwnership.Delete(sceneLoadingEventArgs);
                }

                TransitionPresentationArmed = true;
                TransitionModulesReady = OwnerCore.SceneModuleManager == null;
                TransitionContentReady = OwnerCore.InitializationOptions.SceneContentProvider == null;
                SceneTransitionProgressValue = 0.1f;
                return;
            }

            if (!TransitionModulesReady) {
                if (TransitionModuleOperation == null) {
                    NativeOwnership.Release(ref TransitionModuleOperation);
                    TransitionModuleOperation = OwnerCore.SceneModuleManager.BeginAcquireScene(TransitionTargetSceneIdValue);
                }
                TransitionModuleOperation.AdvanceForBudget(SceneLoadingBudgetMilliseconds, SceneLoadingClock);
                SceneTransitionProgressValue = 0.1f + (0.05f * (float)TransitionModuleOperation.Progress);
                if (TransitionModuleOperation.IsCompleted) {
                    TransitionModuleOperation.CommitSceneLease();
                    NativeOwnership.DisposeAndRelease(ref TransitionModuleOperation);
                    TransitionModulesReady = true;
                }
                return;
            }
            if (!TransitionContentReady) {
                if (!ContentLeases.ContainsKey(TransitionTargetSceneIdValue)) {
                    IRuntimeSceneContentLoadOperation createdContent = BeginContentPreload(TransitionTargetSceneIdValue);
                    ContentLeases.Add(TransitionTargetSceneIdValue, createdContent);
                }
                IRuntimeSceneContentLoadOperation content = ContentLeases[TransitionTargetSceneIdValue];
                content.Advance();
                if (content.IsCompleted) { TransitionContentReady = true; }
                SceneTransitionProgressValue = 0.15f;
                return;
            }

            if (TransitionLoadOperation == null) {
                TransitionSceneAsset = ContentManager.Load<SceneAsset>(TransitionSceneContentPath, RuntimeContentProcessorIds.SceneAsset);
                TransitionLoadOperation = SceneLoadService.CreateTrackedLoadOperation(TransitionSceneAsset);
                SceneTransitionProgressValue = 0.2f;
                return;
            }

            TransitionLoadOperation.AdvanceForBudget(SceneLoadingBudgetMilliseconds, SceneLoadingClock);
            SceneTransitionProgressValue = 0.2f + (0.75f * TransitionLoadOperation.Progress);
            if (!TransitionLoadOperation.IsCompleted) {
                return;
            }

            RuntimeSceneLoadResult loadResult = TransitionLoadOperation.TakeResult();
            try {
                bool dontUnload = TransitionSceneAsset.SceneSettings != null && TransitionSceneAsset.SceneSettings.DontUnload;
                LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(TransitionTargetSceneIdValue, TransitionSceneContentPath, loadResult.RootEntities, loadResult.OwnedAssets, dontUnload);
                LoadedSceneRecord trackedSceneRecord = TrackLoadedSceneRecord(loadedSceneRecord);
                RegisterOwnedAssets(trackedSceneRecord.OwnedAssets, TransitionTargetSceneIdValue);
                SceneLoadedEventArgs sceneLoadedEventArgs = new SceneLoadedEventArgs(trackedSceneRecord.SceneId, trackedSceneRecord.CookedRelativePath, trackedSceneRecord.RootEntities);
                try {
                    DispatchSceneLoaded(sceneLoadedEventArgs);
                    OwnerCore.ReportSceneTransitionStage("AfterSceneLoadedEventDispatch");
                } finally {
                    OwnerCore.ReportSceneTransitionStage("BeforeSceneLoadedEventArgsRelease");
                    NativeOwnership.Delete(sceneLoadedEventArgs);
                    OwnerCore.ReportSceneTransitionStage("AfterSceneLoadedEventArgsRelease");
                }
            } finally {
                // The record now retains the roots and asset set. Dispose the consumed
                // operation before deleting only the caller-owned result wrapper.
                NativeOwnership.DisposeAndRelease(ref TransitionLoadOperation);
                NativeOwnership.Delete(loadResult);
                ReleaseSceneContentPreparation(TransitionTargetSceneIdValue);
            }

            ReleaseTransientSceneAsset(TransitionSceneAsset);
            OwnerCore.ReportSceneTransitionStage("AfterTransitionSceneAssetRelease");
            TransitionSceneAsset = null;
            TransitionSceneContentPath = string.Empty;
            SceneTransitionProgressValue = 1f;
            IsSceneTransitionActiveValue = false;
            OwnerCore.ReportSceneTransitionStage("AfterSceneTransitionCommit");
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
            int loadedSceneRecordIndex = LoadedSceneRecords.IndexOf(loadedSceneRecord);
            if (loadedSceneRecordIndex < 0) {
                throw new InvalidOperationException($"Runtime scene '{sceneId}' is indexed without a load-order record.");
            }
            string loadedSceneId = loadedSceneRecord.SceneId;
            string loadedSceneCookedRelativePath = loadedSceneRecord.CookedRelativePath;

            SceneUnloadingEventArgs sceneUnloadingEventArgs = new SceneUnloadingEventArgs(
                loadedSceneId,
                loadedSceneCookedRelativePath,
                loadedSceneRecord.RootEntities);
            try {
                DispatchSceneUnloading(sceneUnloadingEventArgs);
            } finally {
                NativeOwnership.Delete(sceneUnloadingEventArgs);
            }
            RecordTraceState("UnloadSceneImmediateBeforeDisposeSceneRoots", loadedSceneId);
            DisposeSceneRoots(loadedSceneRecord.RootEntities);
            RecordTraceState("UnloadSceneImmediateBeforeReleaseOwnedAssets", loadedSceneId);
            ReleaseOwnedAssets(loadedSceneRecord.OwnedAssets);
            RecordTraceState("UnloadSceneImmediateAfterReleaseOwnedAssets", loadedSceneId);
            RecordTraceState("UnloadSceneImmediateBeforeRemoveRecordById", loadedSceneId);
            LoadedSceneRecordsById.Remove(loadedSceneId);
            RecordTraceState("UnloadSceneImmediateBeforeRemoveRecord", loadedSceneId);
            NativeOwnership.Delete(loadedSceneRecord.RootEntities);
            NativeOwnership.DisposeAndDelete(loadedSceneRecord.OwnedAssets);
            NativeOwnership.Delete(LoadedSceneRecords[loadedSceneRecordIndex]);
            LoadedSceneRecords.RemoveAt(loadedSceneRecordIndex);
            RecordTraceState("UnloadSceneImmediateBeforeSceneUnloadedEvent", loadedSceneId);
            SceneUnloadedEventArgs sceneUnloadedEventArgs = new SceneUnloadedEventArgs(
                loadedSceneId,
                loadedSceneCookedRelativePath);
            try {
                DispatchSceneUnloaded(sceneUnloadedEventArgs);
            } finally {
                NativeOwnership.Delete(sceneUnloadedEventArgs);
            }
            ReleaseScenePreparation(loadedSceneId);
            RecordTraceState("UnloadSceneImmediateEnd", loadedSceneId);
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
                UnloadSceneImmediate(LoadedSceneRecords[0].SceneId);
            }
            RecordTraceState("UnloadAllScenesEnd", string.Empty);
        }

        /// <summary>
        /// Unloads only the currently tracked scenes that should not survive a single-scene transition.
        /// </summary>
        void UnloadScenesForSingleLoad() {
            int index = 0;
            while (index < LoadedSceneRecords.Count) {
                LoadedSceneRecord loadedSceneRecord = LoadedSceneRecords[index];
                if (loadedSceneRecord.DontUnload) {
                    index++;
                } else {
                    UnloadSceneImmediate(loadedSceneRecord.SceneId);
                }
            }
        }

        /// <summary>
        /// Clears any fixed-step physics backlog carried by the previous scene so the next single-scene load starts without catch-up debt.
        /// </summary>
        void ResetPhysicsTimingForSingleLoad() {
            OwnerCore.ResetPhysicsTimingState();
        }

        /// <summary>
        /// Resolves the content path that should be materialized for one stable scene identifier.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to resolve.</param>
        /// <returns>Content path that should be loaded for the supplied scene id.</returns>
        string ResolveSceneContentPath(string sceneId) {
            RuntimeSceneCatalogEntry entry;
            if (SceneCatalog != null && SceneCatalog.TryGetEntry(sceneId, out entry)) {
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
                ReportEntityDisposalStage("BeforeRootDispose", rootEntities[index], -1);
                NativeOwnership.DisposeAndDelete(rootEntities[index]);
                ReportEntityDisposalStage("AfterRootDispose", null, -1);
            }
        }

        /// <summary>
        /// Disposes live root entities that exist before any built scene has been tracked, such as startup scenes loaded directly by the host.
        /// </summary>
        void DisposeUntrackedRootEntities() {
            List<Entity> rootEntities = new List<Entity>();
            try {
                for (int index = 0; index < ObjectManager.Entities.Count; index++) {
                    Entity entity = ObjectManager.Entities[index];
                    if (entity.Parent == null) {
                        rootEntities.Add(entity);
                    }
                }

                for (int index = rootEntities.Count - 1; index >= 0; index--) {
                    ReportEntityDisposalStage("BeforeUntrackedRootDispose", rootEntities[index], -1);
                    NativeOwnership.DisposeAndDelete(rootEntities[index]);
                    ReportEntityDisposalStage("AfterUntrackedRootDispose", null, -1);
                }
            } finally {
                NativeOwnership.Delete(rootEntities);
            }
        }

        /// <summary>
        /// Releases one transient serialized scene-component record and its payload bytes after materialization completes.
        /// </summary>
        /// <param name="asset">Transient scene-component record to release.</param>
        static void ReleaseTransientSceneComponentAssetRecord(SceneComponentAssetRecord asset) {
            if (asset == null) {
                return;
            }

            byte[] payload = asset.Payload;
            asset.Payload = null;
            DeleteTransientArray(payload);
            asset.MarkReleasedForDiagnostics();
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient platform-only added component asset after materialization completes.
        /// </summary>
        /// <param name="asset">Transient platform-only added component asset to release.</param>
        static void ReleaseTransientSceneEntityPlatformAddedComponentAsset(SceneEntityPlatformAddedComponentAsset asset) {
            if (asset == null) {
                return;
            }

            SceneComponentAssetRecord component = asset.Component;
            asset.Component = null;
            ReleaseTransientSceneComponentAssetRecord(component);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient platform-specific component override asset and all nested authored component data.
        /// </summary>
        /// <param name="asset">Transient platform component override asset to release.</param>
        static void ReleaseTransientSceneEntityPlatformComponentOverrideAsset(SceneEntityPlatformComponentOverrideAsset asset) {
            if (asset == null) {
                return;
            }

            string[] removedComponentKeys = asset.RemovedComponentKeys;
            SceneEntityPlatformAddedComponentAsset[] addedComponents = asset.AddedComponents;
            asset.RemovedComponentKeys = null;
            asset.AddedComponents = null;
            if (addedComponents != null) {
                for (int index = 0; index < addedComponents.Length; index++) {
                    ReleaseTransientSceneEntityPlatformAddedComponentAsset(addedComponents[index]);
                }
            }

            DeleteTransientArray(removedComponentKeys);
            DeleteTransientArray(addedComponents);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient platform-specific transform override asset.
        /// </summary>
        /// <param name="asset">Transient platform transform override asset to release.</param>
        static void ReleaseTransientSceneEntityPlatformTransformOverrideAsset(SceneEntityPlatformTransformOverrideAsset asset) {
            if (asset == null) {
                return;
            }

            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient platform-specific entity existence override asset.
        /// </summary>
        /// <param name="asset">Transient platform existence override asset to release.</param>
        static void ReleaseTransientSceneEntityPlatformExistenceOverrideAsset(SceneEntityPlatformExistenceOverrideAsset asset) {
            if (asset == null) {
                return;
            }

            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient serialized entity asset and all nested scene component and child-entity data.
        /// </summary>
        /// <param name="asset">Transient entity asset to release.</param>
        static void ReleaseTransientSceneEntityAsset(SceneEntityAsset asset) {
            if (asset == null) {
                return;
            }

            SceneComponentAssetRecord[] components = asset.Components;
            SceneEntityPlatformExistenceOverrideAsset[] platformExistenceOverrides = asset.PlatformExistenceOverrides;
            SceneEntityPlatformTransformOverrideAsset[] platformTransformOverrides = asset.PlatformTransformOverrides;
            SceneEntityPlatformComponentOverrideAsset[] platformComponentOverrides = asset.PlatformComponentOverrides;
            SceneEntityAsset[] children = asset.Children;
            SceneOverrideScopeStepKind[] overrideLevelOrder = asset.OverrideLevelOrder;
            asset.Components = null;
            asset.PlatformExistenceOverrides = null;
            asset.PlatformTransformOverrides = null;
            asset.PlatformComponentOverrides = null;
            asset.Children = null;
            asset.OverrideLevelOrder = null;
            if (components != null) {
                for (int index = 0; index < components.Length; index++) {
                    ReleaseTransientSceneComponentAssetRecord(components[index]);
                }
            }
            if (platformExistenceOverrides != null) {
                for (int index = 0; index < platformExistenceOverrides.Length; index++) {
                    ReleaseTransientSceneEntityPlatformExistenceOverrideAsset(platformExistenceOverrides[index]);
                }
            }
            if (platformTransformOverrides != null) {
                for (int index = 0; index < platformTransformOverrides.Length; index++) {
                    ReleaseTransientSceneEntityPlatformTransformOverrideAsset(platformTransformOverrides[index]);
                }
            }
            if (platformComponentOverrides != null) {
                for (int index = 0; index < platformComponentOverrides.Length; index++) {
                    ReleaseTransientSceneEntityPlatformComponentOverrideAsset(platformComponentOverrides[index]);
                }
            }
            if (children != null) {
                for (int index = 0; index < children.Length; index++) {
                    ReleaseTransientSceneEntityAsset(children[index]);
                }
            }

            DeleteTransientArray(components);
            DeleteTransientArray(platformExistenceOverrides);
            DeleteTransientArray(platformTransformOverrides);
            DeleteTransientArray(platformComponentOverrides);
            DeleteTransientArray(children);
            DeleteTransientArray(overrideLevelOrder);
            asset.MarkReleasedForDiagnostics();
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient scene-settings asset and its authored canvas profile.
        /// </summary>
        /// <param name="asset">Transient scene-settings asset to release.</param>
        static void ReleaseTransientSceneSettingsAsset(SceneSettingsAsset asset) {
            if (asset == null) {
                return;
            }

            asset.ReleaseOwnedValuesForNativeDelete();
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Releases one transient serialized scene asset and every nested authored entity and reference payload.
        /// </summary>
        /// <param name="asset">Transient scene asset to release.</param>
        static void ReleaseTransientSceneAsset(SceneAsset asset) {
            if (asset == null) {
                return;
            }

            SceneEntityAsset[] rootEntities = asset.RootEntities;
            SceneAssetReference[] assetReferences = asset.AssetReferences;
            SceneSettingsAsset sceneSettings = asset.SceneSettings;
            asset.RootEntities = null;
            asset.AssetReferences = null;
            if (rootEntities != null) {
                for (int index = 0; index < rootEntities.Length; index++) {
                    ReleaseTransientSceneEntityAsset(rootEntities[index]);
                }
            }
            if (assetReferences != null) {
                for (int index = 0; index < assetReferences.Length; index++) {
                    NativeOwnership.Delete(assetReferences[index]);
                }
            }

            DeleteTransientArray(rootEntities);
            DeleteTransientArray(assetReferences);
            ReleaseTransientSceneSettingsAsset(sceneSettings);
            NativeOwnership.Delete(asset);
        }

        /// <summary>
        /// Deletes one transient array only when it is backed by heap allocation instead of the shared empty-array singleton.
        /// </summary>
        /// <typeparam name="T">Element type stored in the transient array.</typeparam>
        /// <param name="values">Transient array to delete on the native side.</param>
        static void DeleteTransientArray<T>(T[] values) {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }

            NativeOwnership.Delete(values);
        }

        /// <summary>
        /// Registers one scene's owned runtime assets against the active scene set.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned runtime assets resolved during materialization.</param>
        /// <param name="sceneId">Stable identifier of the scene that owns the assets being registered.</param>
        void RegisterOwnedAssets(RuntimeSceneOwnedAssetSet ownedAssets, string sceneId) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedTextures", sceneId);
            OwnedTextureTable.Register(ownedAssets.OwnedTextures);
            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedFonts", sceneId);
            OwnedFontTable.Register(ownedAssets.OwnedFonts);
            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedAudio", sceneId);
            OwnedAudioTable.Register(ownedAssets.OwnedAudio);
            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedModels", sceneId);
            OwnedModelTable.Register(ownedAssets.OwnedModels);
            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedMaterials", sceneId);
            OwnedMaterialTable.Register(ownedAssets.OwnedMaterials);
            RecordTraceState("LoadSceneImmediateBeforeRegisterOwnedAnimationClips", sceneId);
            OwnedAnimationClipTable.Register(ownedAssets.OwnedAnimationClips);
            RecordTraceState("LoadSceneImmediateAfterRegisterOwnedAssets", sceneId);
        }

        /// <summary>
        /// Releases one scene's owned runtime assets when no other loaded scene still references them.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned runtime assets resolved during materialization.</param>
        void ReleaseOwnedAssets(RuntimeSceneOwnedAssetSet ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            OwnedFontTable.Release(ownedAssets.OwnedFonts);
            OwnedAudioTable.Release(ownedAssets.OwnedAudio);
            OwnedTextureTable.Release(ownedAssets.OwnedTextures);
            OwnedModelTable.Release(ownedAssets.OwnedModels);
            OwnedMaterialTable.Release(ownedAssets.OwnedMaterials);
            OwnedAnimationClipTable.Release(ownedAssets.OwnedAnimationClips);
        }

        /// <summary>
        /// Disposes and deletes an animation clip after all referencing scenes have torn down their entities.
        /// </summary>
        /// <param name="ownedAsset">Clip whose final scene reference has been removed.</param>
        void ReleaseOwnedAnimationClip(AnimationClipAsset ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }

            NativeOwnership.DisposeAndDelete(ownedAsset);
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
            if (OwnerCore.RenderManager2D == null) {
                throw new InvalidOperationException("Font asset release requires an initialized 2D render manager.");
            }

            OwnerCore.RenderManager2D.ReleaseFont(ownedAsset);
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
            if (OwnerCore.RenderManager2D == null) {
                throw new InvalidOperationException("Runtime texture release requires an initialized 2D render manager.");
            }

            OwnerCore.RenderManager2D.ReleaseTexture(ownedAsset);
        }

        /// <summary>
        /// Releases one scene-owned audio asset after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned audio asset that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedAudioAsset(AudioAsset ownedAsset) {
            RuntimeSceneAssetReferenceResolver.ReleaseTransientAudioAsset(ownedAsset);
        }

        /// <summary>
        /// Releases one scene-owned runtime model after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned runtime model that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedModel(RuntimeModel ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (OwnerCore.RenderManager3D == null) {
                throw new InvalidOperationException("Runtime model release requires an initialized 3D render manager.");
            }

            OwnerCore.RenderManager3D.ReleaseModel(ownedAsset);
        }

        /// <summary>
        /// Releases one scene-owned runtime material after the final scene reference has been removed.
        /// </summary>
        /// <param name="ownedAsset">Scene-owned runtime material that is no longer referenced by any loaded scene.</param>
        void ReleaseOwnedMaterial(RuntimeMaterial ownedAsset) {
            if (ownedAsset == null) {
                throw new ArgumentNullException(nameof(ownedAsset));
            }
            if (OwnerCore.RenderManager3D == null) {
                throw new InvalidOperationException("Runtime material release requires an initialized 3D render manager.");
            }

            OwnerCore.RenderManager3D.ReleaseMaterial(ownedAsset);
        }

        /// <summary>
        /// Creates one empty owned-asset set for externally tracked scenes whose lifetime remains managed by another host.
        /// </summary>
        /// <returns>Empty owned-asset set.</returns>
        static RuntimeSceneOwnedAssetSet CreateEmptyOwnedAssetSet() {
            return new RuntimeSceneOwnedAssetSet(
                Array.Empty<RuntimeTexture>(),
                Array.Empty<FontAsset>(),
                Array.Empty<AudioAsset>(),
                Array.Empty<RuntimeModel>(),
                Array.Empty<RuntimeMaterial>());
        }

        /// <summary>
        /// Dispatches the scene-loading event while retaining ownership of the event arguments in the calling scope.
        /// </summary>
        /// <param name="eventArgs">Arguments borrowed by scene-loading subscribers for the duration of dispatch.</param>
        void DispatchSceneLoading([NativeNoEscape] SceneLoadingEventArgs eventArgs) {
            SceneLoading?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Dispatches the scene-loaded event while retaining ownership of the event arguments in the calling scope.
        /// </summary>
        /// <param name="eventArgs">Arguments borrowed by scene-loaded subscribers for the duration of dispatch.</param>
        void DispatchSceneLoaded([NativeNoEscape] SceneLoadedEventArgs eventArgs) {
            SceneLoaded?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Dispatches the scene-unloading event while retaining ownership of the event arguments in the calling scope.
        /// </summary>
        /// <param name="eventArgs">Arguments borrowed by scene-unloading subscribers for the duration of dispatch.</param>
        void DispatchSceneUnloading([NativeNoEscape] SceneUnloadingEventArgs eventArgs) {
            SceneUnloading?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Dispatches the scene-unloaded event while retaining ownership of the event arguments in the calling scope.
        /// </summary>
        /// <param name="eventArgs">Arguments borrowed by scene-unloaded subscribers for the duration of dispatch.</param>
        void DispatchSceneUnloaded([NativeNoEscape] SceneUnloadedEventArgs eventArgs) {
            SceneUnloaded?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Flushes any renderer-owned runtime asset releases that were deferred during scene unload before the next scene begins loading.
        /// </summary>
        void FlushReleasedAssets() {
            if (OwnerCore.RenderManager2D == null) {
                throw new InvalidOperationException("Deferred runtime texture release flushing requires an initialized 2D render manager.");
            }

            OwnerCore.RenderManager2D.FlushReleasedTextures();
            if (OwnerCore.RenderManager3D == null) {
                throw new InvalidOperationException("Deferred runtime asset release flushing requires an initialized 3D render manager.");
            }

            OwnerCore.RenderManager3D.FlushReleasedAssets();
            OwnerCore.RenderManager2D.FlushReleasedTextures();
        }

        /// <summary>
        /// Records one focused scene-transition diagnostic snapshot that native hosts can inspect after failures.
        /// </summary>
        /// <param name="stage">Short stage name describing the current transition boundary.</param>
        /// <param name="sceneId">Stable scene identifier associated with the current transition boundary.</param>
        void RecordTraceState(string stage, string sceneId) {
            LastTraceSerial++;
            LastTraceStage = stage;
            LastTraceSceneId = sceneId ?? string.Empty;
            LastTraceLoadedSceneCount = LoadedSceneRecords.Count;
            LastTracePendingOperationCount = PendingOperations.Count;
            OwnerCore.ReportSceneTransitionStage($"SceneManager:{LastTraceStage}");
            SceneTransitionDiagnosticsProvider?.ReportSceneTransitionStage(
                stage,
                LastTraceSceneId,
                LastTraceLoadedSceneCount,
                LastTracePendingOperationCount);
        }

        /// <summary>
        /// Reports one entity disposal stage to the active diagnostics provider when one exists.
        /// </summary>
        /// <param name="stage">Short disposal stage label.</param>
        /// <param name="entity">Entity being disposed.</param>
        /// <param name="componentIndex">Component index involved in the stage, or -1 when not component-specific.</param>
        internal void ReportEntityDisposalStage(string stage, Entity entity, int componentIndex) {
            if (EntityDisposalDiagnosticsProvider == null) {
                return;
            }

            int childCount = entity != null && entity.Children != null ? entity.Children.Count : 0;
            int componentCount = entity != null && entity.Components != null ? entity.Components.Count : 0;
            EntityDisposalDiagnosticsProvider.ReportEntityDisposalStage(stage, childCount, componentCount, componentIndex);
        }

        /// <summary>Releases manager-owned scheduling resources; scene roots and assets retain their existing explicit unload lifecycle.</summary>
        public void Dispose() {
            NativeOwnership.DisposeAndRelease(ref TransitionLoadOperation);
            NativeOwnership.DisposeAndRelease(ref TransitionModuleOperation);
            if (SceneLoadingClock != null) {
                if (TransitionSceneAsset != null) { ReleaseTransientSceneAsset(TransitionSceneAsset); TransitionSceneAsset = null; }
                if (!LoadedSceneRecordsById.ContainsKey(TransitionTargetSceneIdValue)) { ReleaseScenePreparation(TransitionTargetSceneIdValue); }
                while (LoadedSceneRecords.Count != 0) { UnloadSceneImmediate(LoadedSceneRecords[LoadedSceneRecords.Count - 1].SceneId); }
            }
            NativeOwnership.DisposeAndRelease(ref SceneLoadingClock);
            NativeOwnership.Release(ref ContentLeases);
        }

        /// <summary>Acquires optional preparations without waiting for pending platform IO in the synchronous API.</summary>
        /// <param name="sceneId">Scene whose exact dependency catalog is being acquired.</param>
        void PrepareSceneSynchronously(string sceneId) {
            SceneModuleLoadOperation modules = null;
            IRuntimeSceneContentLoadOperation content = null;
            try {
                if (OwnerCore.SceneModuleManager != null) {
                    modules = OwnerCore.SceneModuleManager.BeginAcquireScene(sceneId);
                    while (!modules.IsCompleted) {
                        double before = modules.Progress;
                        modules.Advance();
                        if (!modules.IsCompleted && modules.Progress == before) { throw new InvalidOperationException("Pending native code IO requires RequestSceneTransition instead of synchronous LoadScene."); }
                    }
                    modules.CommitSceneLease();
                }
                if (OwnerCore.InitializationOptions.SceneContentProvider != null) {
                    content = BeginContentPreload(sceneId);
                    content.Advance();
                    if (!content.IsCompleted) { throw new InvalidOperationException("Pending scene content IO requires RequestSceneTransition instead of synchronous LoadScene."); }
                    ContentLeases.Add(sceneId, content);
                    content = null;
                }
            } finally {
                NativeOwnership.DisposeAndDelete(modules);
                NativeOwnership.DisposeAndDelete(content);
            }
        }

        /// <summary>Starts exact scene preloading without converting missing content into a fallback stream.</summary>
        /// <param name="sceneId">Exact packaged scene identity.</param>
        /// <returns>Owned resident-adapter operation retaining the scene's content cache.</returns>
        [NativeOwnedReturn]
        IRuntimeSceneContentLoadOperation BeginContentPreload(string sceneId) {
            if (SceneCatalog == null || !SceneCatalog.TryGetEntry(sceneId, out RuntimeSceneCatalogEntry entry)) { throw new InvalidOperationException($"Scene '{sceneId}' requires an exact packaged content catalog entry."); }
            IRuntimeSceneContentLoadOperation operation = OwnerCore.InitializationOptions.SceneContentProvider.BeginPreloadScene(entry);
            if (operation == null) { throw new InvalidOperationException("Scene content providers must return an owned preload operation."); }
            return operation;
        }

        /// <summary>Ends the serialized read scope once preparation and loaded callbacks have consumed it; platform streams retain their own outstanding borrows.</summary>
        /// <param name="sceneId">Scene whose content preparation scope has ended.</param>
        void ReleaseSceneContentPreparation(string sceneId) {
            if (ContentLeases != null && ContentLeases.TryGetValue(sceneId, out IRuntimeSceneContentLoadOperation content)) {
                content.Dispose();
                ContentLeases.Remove(sceneId);
            }
        }

        /// <summary>Retires GPU work and any incomplete content preparation before releasing the code implementing scene resources and callbacks.</summary>
        /// <param name="sceneId">Scene whose objects and unload notifications have completed.</param>
        void ReleaseScenePreparation(string sceneId) {
            bool hasContent = ContentLeases != null && ContentLeases.ContainsKey(sceneId);
            bool hasModules = OwnerCore.SceneModuleManager != null && OwnerCore.SceneModuleManager.HasSceneLease(sceneId);
            if (hasContent || hasModules) { FlushReleasedAssets(); }
            ReleaseSceneContentPreparation(sceneId);
            if (OwnerCore.SceneModuleManager != null && OwnerCore.SceneModuleManager.HasSceneLease(sceneId)) { OwnerCore.SceneModuleManager.ReleaseScene(sceneId); }
        }
    }
}
