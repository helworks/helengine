namespace helengine.editor {
    /// <summary>
    /// Reconstructs editor entities from serialized scene asset payloads.
    /// </summary>
    public class SceneLoadService {
        /// <summary>
        /// Registry used to deserialize supported component types.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;

        /// <summary>
        /// Resolver used to rebuild runtime assets referenced by persisted components.
        /// </summary>
        readonly ISceneAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Initializes a new scene load service.
        /// </summary>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime assets.</param>
        public SceneLoadService(ComponentPersistenceRegistry persistenceRegistry, ISceneAssetReferenceResolver referenceResolver) {
            PersistenceRegistry = persistenceRegistry ?? throw new ArgumentNullException(nameof(persistenceRegistry));
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        }

        /// <summary>
        /// Loads root editor entities from one serialized scene asset payload.
        /// </summary>
        /// <param name="sceneAsset">Scene asset payload to materialize.</param>
        /// <returns>Loaded root editor entities.</returns>
        public IReadOnlyList<EditorEntity> Load(SceneAsset sceneAsset) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            List<EditorEntity> loadedRoots = new List<EditorEntity>(rootEntities.Length);
            for (int i = 0; i < rootEntities.Length; i++) {
                loadedRoots.Add(LoadEntity(rootEntities[i]));
            }

            return loadedRoots;
        }

        /// <summary>
        /// Loads one serialized scene entity recursively.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload to materialize.</param>
        /// <returns>Loaded editor entity.</returns>
        EditorEntity LoadEntity(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            EditorEntity entity = new EditorEntity {
                Name = entityAsset.Name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation
            };

            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int i = 0; i < componentRecords.Length; i++) {
                SceneComponentAssetRecord record = componentRecords[i];
                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
                Component component = descriptor.DeserializeComponent(record, saveComponent, ReferenceResolver);
                entity.AddComponent(component);
            }

            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int i = 0; i < children.Length; i++) {
                entity.AddChild(LoadEntity(children[i]));
            }

            return entity;
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component when present; otherwise null.</returns>
        EntitySaveComponent FindEntitySaveComponent(EditorEntity entity) {
            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }
    }
}
