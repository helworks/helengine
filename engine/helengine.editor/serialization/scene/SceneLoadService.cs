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
        /// Factory used to create authored scene entities for the active editor host.
        /// </summary>
        readonly IEntityFactory EntityFactory;

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
            EntityFactory = new EditorEntityFactory();
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

            EditorEntity entity = ResolveEditorEntity(EntityFactory.Create(entityAsset.Name));
            entity.LocalPosition = entityAsset.LocalPosition;
            entity.LocalScale = entityAsset.LocalScale;
            entity.LocalOrientation = entityAsset.LocalOrientation;

            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            if (string.IsNullOrWhiteSpace(entityAsset.Id)) {
                throw new InvalidOperationException("Serialized scene entities must define a stable id.");
            }

            EntityReferenceTable.RegisterEntity(entity, entityAsset.Id);
            if (saveComponent != null) {
                saveComponent.EntityId = entityAsset.Id;
                RestoreEntityTransformPlatformOverrides(entityAsset, saveComponent);
            }

            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int i = 0; i < componentRecords.Length; i++) {
                SceneComponentAssetRecord record = componentRecords[i];
                IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(record.ComponentTypeId);
                SceneComponentAssetRecord baseRecord = OverridePayloadService.UnwrapBaseRecord(record);
                Component component = descriptor.DeserializeComponent(baseRecord, saveComponent, ReferenceResolver);
                entity.AddComponent(component);
                RestoreComponentKey(baseRecord, saveComponent, component);
                RestorePlatformOverrides(record, saveComponent, component);
            }

            if (saveComponent != null) {
                RestoreEntityComponentPlatformOverrides(entityAsset, saveComponent);
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
        /// Restores the stable editor component key for one deserialized common live component.
        /// </summary>
        /// <param name="record">Serialized scene component record that may define a stable component key.</param>
        /// <param name="saveComponent">Hidden entity save component that owns the restored metadata.</param>
        /// <param name="component">Live component reconstructed from the serialized record.</param>
        void RestoreComponentKey(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, Component component) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (saveComponent == null || string.IsNullOrWhiteSpace(record.ComponentKey)) {
                return;
            }

            saveComponent.GetOrCreateComponentState(component).ComponentKey = record.ComponentKey;
        }

        /// <summary>
        /// Restores serialized entity transform override metadata into the hidden save component after the base entity initializes.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload that may contain transform overrides.</param>
        /// <param name="saveComponent">Hidden entity save component that owns the editor transform metadata.</param>
        void RestoreEntityTransformPlatformOverrides(SceneEntityAsset entityAsset, EntitySaveComponent saveComponent) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            SceneEntityPlatformTransformOverrideAsset[] overrideAssets = entityAsset.PlatformTransformOverrides ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            for (int index = 0; index < overrideAssets.Length; index++) {
                SceneEntityPlatformTransformOverrideAsset overrideAsset = overrideAssets[index];
                if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
                    continue;
                }

                saveComponent.SetTransformPlatformOverride(overrideAsset.PlatformId, new SceneEntityPlatformTransformOverrideAsset {
                    PlatformId = overrideAsset.PlatformId,
                    HasLocalPositionOverride = overrideAsset.HasLocalPositionOverride,
                    LocalPosition = overrideAsset.LocalPosition,
                    HasLocalScaleOverride = overrideAsset.HasLocalScaleOverride,
                    LocalScale = overrideAsset.LocalScale,
                    HasLocalOrientationOverride = overrideAsset.HasLocalOrientationOverride,
                    LocalOrientation = overrideAsset.LocalOrientation
                });
            }
        }

        /// <summary>
        /// Restores serialized entity component existence override metadata into the hidden save component after the base entity initializes.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload that may contain component existence overrides.</param>
        /// <param name="saveComponent">Hidden entity save component that owns the editor component metadata.</param>
        void RestoreEntityComponentPlatformOverrides(SceneEntityAsset entityAsset, EntitySaveComponent saveComponent) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            SceneEntityPlatformComponentOverrideAsset[] overrideAssets = entityAsset.PlatformComponentOverrides ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
            for (int platformIndex = 0; platformIndex < overrideAssets.Length; platformIndex++) {
                SceneEntityPlatformComponentOverrideAsset overrideAsset = overrideAssets[platformIndex];
                if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
                    continue;
                }

                EntityPlatformComponentOverrideState componentOverrideState = saveComponent.GetOrCreateComponentPlatformOverride(overrideAsset.PlatformId);
                string[] removedComponentKeys = overrideAsset.RemovedComponentKeys ?? Array.Empty<string>();
                for (int removedIndex = 0; removedIndex < removedComponentKeys.Length; removedIndex++) {
                    if (!string.IsNullOrWhiteSpace(removedComponentKeys[removedIndex])) {
                        componentOverrideState.MarkComponentRemoved(removedComponentKeys[removedIndex]);
                    }
                }

                SceneEntityPlatformAddedComponentAsset[] addedComponents = overrideAsset.AddedComponents ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>();
                for (int addedIndex = 0; addedIndex < addedComponents.Length; addedIndex++) {
                    EntityPlatformAddedComponentState addedComponentState = LoadAddedComponentState(addedComponents[addedIndex]);
                    if (addedComponentState != null) {
                        componentOverrideState.SetAddedComponent(addedComponentState);
                    }
                }
            }
        }

        /// <summary>
        /// Reconstructs one detached platform-only component state from serialized scene metadata.
        /// </summary>
        /// <param name="addedComponentAsset">Serialized platform-only component payload.</param>
        /// <returns>Detached platform-only component state when one exists; otherwise null.</returns>
        EntityPlatformAddedComponentState LoadAddedComponentState(SceneEntityPlatformAddedComponentAsset addedComponentAsset) {
            if (addedComponentAsset == null || addedComponentAsset.Component == null) {
                return null;
            }

            SceneComponentAssetRecord componentRecord = addedComponentAsset.Component;
            IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(componentRecord.ComponentTypeId);
            EntitySaveComponent detachedSaveComponent = new EntitySaveComponent();
            Component detachedComponent = descriptor.DeserializeComponent(componentRecord, detachedSaveComponent, ReferenceResolver);
            if (!detachedSaveComponent.TryGetComponentState(detachedComponent, out EntityComponentSaveState saveState)) {
                saveState = detachedSaveComponent.GetOrCreateComponentState(detachedComponent);
            }
            saveState.ComponentKey = componentRecord.ComponentKey;

            return new EntityPlatformAddedComponentState {
                ComponentKey = componentRecord.ComponentKey,
                Component = detachedComponent,
                SaveState = saveState
            };
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

        /// <summary>
        /// Resolves the editor entity returned by the host-owned authored entity factory.
        /// </summary>
        /// <param name="entity">Entity returned by the factory.</param>
        /// <returns>Resolved editor entity.</returns>
        EditorEntity ResolveEditorEntity(Entity entity) {
            if (entity is EditorEntity editorEntity) {
                return editorEntity;
            }

            throw new InvalidOperationException("Editor scene load requires the entity factory to return EditorEntity instances.");
        }
    }
}


