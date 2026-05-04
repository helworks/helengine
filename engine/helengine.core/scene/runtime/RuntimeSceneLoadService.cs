namespace helengine {
    /// <summary>
    /// Materializes packaged scene assets into live runtime entities for player builds.
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
        /// Loads root runtime entities from one packaged scene asset.
        /// </summary>
        /// <param name="sceneAsset">Packaged scene asset payload to materialize.</param>
        /// <returns>Loaded root runtime entities.</returns>
        public IReadOnlyList<Entity> Load(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            Logger.WriteLine("Loading packaged scene assets.");
            System.Diagnostics.Stopwatch loadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            List<Entity> rootEntities = new List<Entity>(rootEntityAssets.Length);
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                rootEntities.Add(LoadEntity(rootEntityAssets[index]));
            }

            loadStopwatch.Stop();
            Logger.WriteLine($"Loaded packaged scene assets in {loadStopwatch.Elapsed.TotalMilliseconds:0.###} ms ({rootEntities.Count} root entities).");

            return rootEntities;
        }

        /// <summary>
        /// Loads one serialized runtime entity recursively.
        /// </summary>
        /// <param name="entityAsset">Serialized runtime entity payload to materialize.</param>
        /// <returns>Loaded runtime entity.</returns>
        Entity LoadEntity(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            Entity entity = new Entity {
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation
            };
            entity.InitComponents();
            entity.InitChildren();

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                entity.AddComponent(LoadComponent(componentRecords[index]));
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                entity.AddChild(LoadEntity(childEntityAssets[index]));
            }

            return entity;
        }

        /// <summary>
        /// Loads one serialized runtime component from its scene record.
        /// </summary>
        /// <param name="record">Serialized component record to materialize.</param>
        /// <returns>Loaded runtime component.</returns>
        Component LoadComponent(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            return ComponentRegistry.GetDeserializer(record.ComponentTypeId).Deserialize(record, ReferenceResolver);
        }
    }
}
