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
        /// Tracks stable entity ids for the current load session.
        /// </summary>
        readonly SceneEntityReferenceTable EntityReferenceTable;

        /// <summary>
        /// Service that unwraps editor-only component platform override metadata from serialized component payloads.
        /// </summary>
        readonly ComponentPlatformOverridePayloadService OverridePayloadService;

        /// <summary>
        /// Initializes a new scene load service.
        /// </summary>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime assets.</param>
        public SceneLoadService(ComponentPersistenceRegistry persistenceRegistry, ISceneAssetReferenceResolver referenceResolver) {
            PersistenceRegistry = persistenceRegistry ?? throw new ArgumentNullException(nameof(persistenceRegistry));
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            EntityReferenceTable = new SceneEntityReferenceTable();
            OverridePayloadService = new ComponentPlatformOverridePayloadService();
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

            ComponentExecutionContext.EnterEditor();
            try {
                SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
                List<EditorEntity> loadedRoots = new List<EditorEntity>(rootEntities.Length);
                for (int i = 0; i < rootEntities.Length; i++) {
                    loadedRoots.Add(LoadEntity(rootEntities[i]));
                }

                return loadedRoots;
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
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
                SuppressUpdateComponentExecutionInEditor = true,
                LocalPosition = entityAsset.LocalPosition,
                LocalScale = entityAsset.LocalScale,
                LocalOrientation = entityAsset.LocalOrientation
            };

            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            if (string.IsNullOrWhiteSpace(entityAsset.Id)) {
                throw new InvalidOperationException("Serialized scene entities must define a stable id.");
            }

            EntityReferenceTable.RegisterEntity(entity, entityAsset.Id);
            if (saveComponent != null) {
                saveComponent.EntityId = entityAsset.Id;
            }

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int i = 0; i < componentRecords.Length; i++) {
                SceneComponentAssetRecord record = componentRecords[i];
                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
                SceneComponentAssetRecord baseRecord = OverridePayloadService.UnwrapBaseRecord(record);
                Component component = descriptor.DeserializeComponent(baseRecord, saveComponent, ReferenceResolver);
                entity.AddComponent(component);
                RestorePlatformOverrides(record, saveComponent, component);
            }

            EditorSceneCameraSuppressionService.AttachAndSuppress(entity);
            EditorCameraVisualAttachmentService.Attach(entity);
            EditorPointLightVisualAttachmentService.Attach(entity);
            EditorDirectionalLightVisualAttachmentService.Attach(entity);
            EditorSpotLightVisualAttachmentService.Attach(entity);

            SceneEntityAsset[] children = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int i = 0; i < children.Length; i++) {
                entity.AddChild(LoadEntity(children[i]));
            }

            return entity;
        }

        /// <summary>
        /// Restores editor-only component platform override metadata into the hidden save component after the base component loads.
        /// </summary>
        /// <param name="persistedRecord">Serialized component record that may contain platform override metadata.</param>
        /// <param name="saveComponent">Hidden entity save component that owns the component save-state.</param>
        /// <param name="component">Loaded live base component instance.</param>
        void RestorePlatformOverrides(SceneComponentAssetRecord persistedRecord, EntitySaveComponent saveComponent, Component component) {
            if (persistedRecord == null) {
                throw new ArgumentNullException(nameof(persistedRecord));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (saveComponent == null) {
                return;
            }

            IReadOnlyList<EntityComponentPlatformOverrideState> overrideStates = OverridePayloadService.ReadOverrideStates(persistedRecord);
            if (overrideStates.Count < 1) {
                return;
            }

            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            for (int index = 0; index < overrideStates.Count; index++) {
                saveState.SetPlatformOverride(overrideStates[index].PlatformId, overrideStates[index]);
            }
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
