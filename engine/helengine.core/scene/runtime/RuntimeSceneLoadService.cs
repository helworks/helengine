namespace helengine {
    /// <summary>
    /// Materializes packaged scene assets into live runtime entities for <see cref="SceneManager"/> and explicit raw-load test seams.
    /// </summary>
    public sealed class RuntimeSceneLoadService {
        /// <summary>
        /// Resolver used to rebuild runtime assets referenced by packaged scene records.
        /// </summary>
        readonly RuntimeSceneAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Registry used to deserialize runtime component records.
        /// </summary>
        readonly RuntimeComponentRegistry ComponentRegistry;

        /// <summary>
        /// Gets the most recent runtime scene-load stage recorded for diagnostics.
        /// </summary>
        public string LastTraceStage { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the current root-entity index being materialized for diagnostics.
        /// </summary>
        public int LastTraceRootEntityIndex { get; private set; }

        /// <summary>
        /// Gets the current child-entity depth being materialized for diagnostics.
        /// </summary>
        public int LastTraceEntityDepth { get; private set; }

        /// <summary>
        /// Gets the current component type id being materialized for diagnostics.
        /// </summary>
        public string LastTraceComponentTypeId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the last recorded text-load stage emitted by the shared scene asset reference resolver.
        /// </summary>
        public string LastTextLoadStage => ReferenceResolver.LastTextLoadStage;

        /// <summary>
        /// Gets the last recorded text-font relative path emitted by the shared scene asset reference resolver.
        /// </summary>
        public string LastTextFontRelativePath => ReferenceResolver.LastTextFontRelativePath;

        /// <summary>
        /// Gets the last recorded texture-load stage emitted by the shared scene asset reference resolver.
        /// </summary>
        public string LastTextureLoadStage => ReferenceResolver.LastTextureLoadStage;

        /// <summary>
        /// Gets the last recorded packaged texture relative path emitted by the shared scene asset reference resolver.
        /// </summary>
        public string LastTextureRelativePath => ReferenceResolver.LastTextureRelativePath;

        /// <summary>
        /// Gets the most recent packaged font-deserialization stage emitted by the shared scene asset reference resolver.
        /// </summary>
        public string LastFontDeserializeStage => ReferenceResolver.LastFontDeserializeStage;

        /// <summary>
        /// Initializes a new runtime scene-load service with the default runtime component registry.
        /// </summary>
        /// <param name="referenceResolver">Resolver used to rebuild packaged runtime assets.</param>
        public RuntimeSceneLoadService(RuntimeSceneAssetReferenceResolver referenceResolver)
        {
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            ComponentRegistry = RuntimeComponentRegistry.CreateDefault();
        }

        /// <summary>
        /// Initializes a new runtime scene-load service.
        /// </summary>
        /// <param name="referenceResolver">Resolver used to rebuild packaged runtime assets.</param>
        /// <param name="componentRegistry">Registry used to deserialize packaged runtime components.</param>
        public RuntimeSceneLoadService(
            RuntimeSceneAssetReferenceResolver referenceResolver,
            RuntimeComponentRegistry componentRegistry) {
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            ComponentRegistry = componentRegistry ?? throw new ArgumentNullException(nameof(componentRegistry));
        }

        /// <summary>
        /// Loads root runtime entities together with scene-owned runtime assets from one packaged scene asset.
        /// </summary>
        /// <param name="sceneAsset">Packaged scene asset payload to materialize.</param>
        /// <returns>Loaded runtime entities together with scene-owned runtime assets.</returns>
        public RuntimeSceneLoadResult LoadTracked(SceneAsset sceneAsset) {
            RuntimeSceneLoadOperation operation = CreateTrackedLoadOperation(sceneAsset);
            while (!operation.IsCompleted) {
                operation.Advance();
            }

            return operation.Result;
        }

        /// <summary>
        /// Creates one resumable operation that materializes a packaged scene while tracking its owned runtime assets.
        /// </summary>
        /// <param name="sceneAsset">Packaged scene payload to materialize.</param>
        /// <returns>Operation that materializes one root entity per advance.</returns>
        public RuntimeSceneLoadOperation CreateTrackedLoadOperation(SceneAsset sceneAsset) {
            return new RuntimeSceneLoadOperation(this, sceneAsset);
        }

        /// <summary>
        /// Starts owned runtime-asset tracking for one incremental scene-load operation.
        /// </summary>
        internal void BeginTrackedLoad() {
            ReferenceResolver.BeginOwnedAssetTracking();
        }

        /// <summary>
        /// Finalizes owned runtime-asset tracking for one completed incremental scene-load operation.
        /// </summary>
        /// <returns>Assets acquired while materializing the operation.</returns>
        internal RuntimeSceneOwnedAssetSet CompleteTrackedLoad() {
            return ReferenceResolver.CompleteOwnedAssetTracking();
        }

        /// <summary>
        /// Tracks a runtime model created during load-time component preparation so the loaded scene releases it during unload.
        /// </summary>
        /// <param name="runtimeModel">Prepared runtime model owned by the active load operation.</param>
        internal void TrackPreparedModel(RuntimeModel runtimeModel) {
            ReferenceResolver.TrackAdditionalOwnedModel(runtimeModel);
        }

        /// <summary>
        /// Materializes one serialized root entity for an incremental scene-load operation.
        /// </summary>
        /// <param name="entityAsset">Serialized root entity payload.</param>
        /// <param name="rootEntityIndex">Index of the root entity in the packaged scene.</param>
        /// <returns>Materialized runtime root entity.</returns>
        internal Entity LoadRootEntity(SceneEntityAsset entityAsset, int rootEntityIndex) {
            return LoadEntity(entityAsset, rootEntityIndex, 0);
        }

        /// <summary>
        /// Loads root runtime entities from one packaged scene asset.
        /// </summary>
        /// <param name="sceneAsset">Packaged scene asset payload to materialize.</param>
        /// <returns>Loaded root runtime entities.</returns>
        public IReadOnlyList<Entity> Load(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            RecordTraceState("LoadBegin", -1, 0, string.Empty);
            System.Diagnostics.Stopwatch loadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            List<Entity> rootEntities = new List<Entity>(rootEntityAssets.Length);
            try {
                for (int index = 0; index < rootEntityAssets.Length; index++) {
                    RecordTraceState("BeforeRootEntityLoad", index, 0, string.Empty);
                    rootEntities.Add(LoadEntity(rootEntityAssets[index], index, 0));
                }
                for (int index = 0; index < rootEntities.Count; index++) {
                    rootEntities[index].InitializeHierarchy();
                }

                loadStopwatch.Stop();
                RecordTraceState("LoadEnd", rootEntities.Count - 1, 0, string.Empty);

                return rootEntities;
            } finally {
                NativeOwnership.Delete(loadStopwatch);
            }
        }

        /// <summary>
        /// Loads one serialized runtime entity recursively.
        /// </summary>
        /// <param name="entityAsset">Serialized runtime entity payload to materialize.</param>
        /// <returns>Loaded runtime entity.</returns>
        Entity LoadEntity(SceneEntityAsset entityAsset, int rootEntityIndex, int entityDepth) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            RecordTraceState("LoadEntityBegin", rootEntityIndex, entityDepth, string.Empty);
            Entity entity = new Entity {
                Static = entityAsset.IsStatic,
                Enabled = entityAsset.Enabled,
                LayerMask = entityAsset.LayerMask,
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation
            };
            entity.InitComponents();
            entity.InitChildren();
            if (entityAsset.Id != 0u) {
                entity.AddComponent(new SceneEntityRuntimeIdComponent {
                    SceneEntityId = entityAsset.Id
                });
            }

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                RecordTraceState("BeforeComponentLoad", rootEntityIndex, entityDepth, componentRecords[index] != null ? componentRecords[index].ComponentTypeId : string.Empty);
                entity.AddComponent(LoadComponent(componentRecords[index], rootEntityIndex, entityDepth));
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                RecordTraceState("BeforeChildEntityLoad", rootEntityIndex, entityDepth + 1, string.Empty);
                entity.AddChild(LoadEntity(childEntityAssets[index], rootEntityIndex, entityDepth + 1));
            }

            RecordTraceState("LoadEntityEnd", rootEntityIndex, entityDepth, string.Empty);
            return entity;
        }

        /// <summary>
        /// Loads one serialized runtime component from its scene record.
        /// </summary>
        /// <param name="record">Serialized component record to materialize.</param>
        /// <returns>Loaded runtime component.</returns>
        Component LoadComponent(SceneComponentAssetRecord record, int rootEntityIndex, int entityDepth) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            RecordTraceState("LoadComponentBegin", rootEntityIndex, entityDepth, record.ComponentTypeId);
            return ComponentRegistry.GetDeserializer(record.ComponentTypeId).Deserialize(record, ReferenceResolver);
        }

        /// <summary>
        /// Records one runtime scene-load diagnostic snapshot that native hosts can inspect after failures.
        /// </summary>
        /// <param name="stage">Short scene-load stage name.</param>
        /// <param name="rootEntityIndex">Current root-entity index under materialization.</param>
        /// <param name="entityDepth">Current entity depth under materialization.</param>
        /// <param name="componentTypeId">Current component type id under materialization.</param>
        void RecordTraceState(string stage, int rootEntityIndex, int entityDepth, string componentTypeId) {
            LastTraceStage = stage;
            LastTraceRootEntityIndex = rootEntityIndex;
            LastTraceEntityDepth = entityDepth;
            LastTraceComponentTypeId = componentTypeId ?? string.Empty;
            Core.Instance?.ReportSceneTransitionStage($"SceneLoad:{LastTraceStage}");
        }
    }
}
