namespace helengine.editor {
    /// <summary>
    /// Reconstructs editor entities from serialized scene asset payloads.
    /// </summary>
    public class SceneLoadService : IDisposable {
        /// <summary>
        /// Registry used to deserialize supported component types.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;

        /// <summary>
        /// Resolver used to rebuild runtime assets referenced by persisted components.
        /// </summary>
        readonly ISceneAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Tracks stable entity ids for the active load invocation.
        /// </summary>
        SceneEntityReferenceTable EntityReferenceTable;

        /// <summary>
        /// Service that unwraps editor-only component platform override metadata from serialized component payloads.
        /// </summary>
        readonly ComponentPlatformOverridePayloadService OverridePayloadService;

        /// <summary>
        /// Factory used to create authored scene entities for the active editor host.
        /// </summary>
        readonly IEntityFactory EntityFactory;
        /// <summary>
        /// Allocator that advances beyond restored numeric scene entity ids during scene load.
        /// </summary>
        readonly EditorSceneEntityIdAllocator EntityIdAllocator;
        /// <summary>
        /// Optional blueprint expansion service used when scene instance roots reference blueprint assets.
        /// </summary>
        readonly BlueprintEditorExpansionService BlueprintExpansionService;

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
            EntityFactory = ResolveEntityFactory();
            EntityIdAllocator = ResolveEntityIdAllocator();
            BlueprintExpansionService = null;
        }

        /// <summary>
        /// Initializes a new scene load service with blueprint instance expansion enabled for one project root.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime assets.</param>
        public SceneLoadService(
            string projectRootPath,
            ComponentPersistenceRegistry persistenceRegistry,
            ISceneAssetReferenceResolver referenceResolver) : this(persistenceRegistry, referenceResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            BlueprintExpansionService = new BlueprintEditorExpansionService(projectRootPath, persistenceRegistry, referenceResolver);
        }

        /// <summary>
        /// Releases blueprint-expansion resolver state owned by this loader.
        /// </summary>
        public void Dispose() {
            BlueprintExpansionService?.Dispose();
        }

        /// <summary>
        /// Resolves the host-owned authored entity factory from the active core instance.
        /// </summary>
        /// <returns>Host-owned authored entity factory.</returns>
        static IEntityFactory ResolveEntityFactory() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("Scene loading requires Core.Instance before resolving EntityFactory.");
            } else if (Core.Instance.EntityFactory == null) {
                throw new InvalidOperationException("Scene loading requires Core.Instance.EntityFactory.");
            }

            return Core.Instance.EntityFactory;
        }

        /// <summary>
        /// Resolves the editor-owned scene entity id allocator from the active editor core.
        /// </summary>
        /// <returns>Allocator that owns numeric scene entity ids for the active editor host.</returns>
        static EditorSceneEntityIdAllocator ResolveEntityIdAllocator() {
            if (Core.Instance is not EditorCore editorCore) {
                throw new InvalidOperationException("Scene loading requires EditorCore before resolving the scene entity id allocator.");
            } else if (editorCore.SceneEntityIdAllocator == null) {
                throw new InvalidOperationException("Scene loading requires EditorCore.SceneEntityIdAllocator.");
            }

            return editorCore.SceneEntityIdAllocator;
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

            EntityReferenceTable = new SceneEntityReferenceTable();
            ComponentExecutionContext.EnterEditor();
            try {
                SceneEntityAsset[] rootEntities = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
                List<EditorEntity> loadedRoots = new List<EditorEntity>(rootEntities.Length);
                for (int i = 0; i < rootEntities.Length; i++) {
                    loadedRoots.Add(LoadEntity(rootEntities[i]));
                }
                for (int i = 0; i < loadedRoots.Count; i++) {
                    loadedRoots[i].InitializeHierarchy();
                }

                return loadedRoots;
            } finally {
                ComponentExecutionContext.ExitEditor();
            }
        }

        /// <summary>
        /// Expands one live blueprint instance root into inherited children when blueprint expansion is enabled.
        /// </summary>
        /// <param name="instanceRoot">Scene-owned blueprint instance root to expand.</param>
        public void ExpandBlueprintInstanceRoot(EditorEntity instanceRoot) {
            if (instanceRoot == null) {
                throw new ArgumentNullException(nameof(instanceRoot));
            }

            if (BlueprintExpansionService == null) {
                return;
            }

            BlueprintExpansionService.ExpandInstanceRoot(instanceRoot);
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
            entity.IsSceneOwned = true;
            entity.Static = entityAsset.IsStatic;
            entity.Enabled = entityAsset.Enabled;
            entity.LayerMask = entityAsset.LayerMask;
            entity.LocalPosition = entityAsset.LocalPosition;
            entity.LocalScale = entityAsset.LocalScale;
            entity.LocalOrientation = entityAsset.LocalOrientation;

            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            if (entityAsset.Id == 0u) {
                throw new InvalidOperationException("Serialized scene entities must define a non-zero stable id.");
            }

            EntityIdAllocator.RegisterRestored(entityAsset.Id);
            if (saveComponent != null) {
                saveComponent.EntityId = entityAsset.Id;
                RestoreEntityExistencePlatformOverrides(entityAsset, saveComponent);
                RestoreEntityTransformPlatformOverrides(entityAsset, saveComponent);
            }
            EntityReferenceTable.RegisterEntity(entity, entityAsset.Id);
            AttachSceneEntityRuntimeId(entity, entityAsset.Id);

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

            if (BlueprintExpansionService != null && FindBlueprintInstanceComponent(entity) != null) {
                BlueprintExpansionService.ExpandInstanceRoot(entity);
            }

            return entity;
        }

        /// <summary>
        /// Attaches the runtime-facing stable scene-entity id component required by gameplay systems that resolve authored scene references during editor scene preview.
        /// </summary>
        /// <param name="entity">Loaded editor entity that should expose the authored scene id.</param>
        /// <param name="sceneEntityId">Stable serialized scene entity id restored from the scene asset.</param>
        static void AttachSceneEntityRuntimeId(EditorEntity entity, uint sceneEntityId) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (sceneEntityId == 0u) {
                throw new ArgumentOutOfRangeException(nameof(sceneEntityId), "Loaded editor entities must expose a non-zero serialized scene entity id.");
            }

            entity.AddComponent(new SceneEntityRuntimeIdComponent {
                SceneEntityId = sceneEntityId
            });
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
            HashSet<EditorOverrideScope> scopes = new HashSet<EditorOverrideScope>();
            for (int index = 0; index < overrideStates.Count; index++) {
                EntityComponentPlatformOverrideState overrideState = overrideStates[index];
                EditorOverrideScope scope = new EditorOverrideScope(overrideState.PlatformId, overrideState.EnvironmentId);
                if (!scopes.Add(scope)) {
                    throw new InvalidOperationException($"Duplicate component override scope '{scope}'.");
                }

                saveState.SetScopedPlatformOverride(scope, overrideState);
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
        /// Restores serialized entity existence override metadata into the hidden save component after the base entity initializes.
        /// </summary>
        /// <param name="entityAsset">Serialized entity payload that may contain existence overrides.</param>
        /// <param name="saveComponent">Hidden entity save component that owns the editor existence metadata.</param>
        void RestoreEntityExistencePlatformOverrides(SceneEntityAsset entityAsset, EntitySaveComponent saveComponent) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            SceneEntityPlatformExistenceOverrideAsset[] overrideAssets = entityAsset.PlatformExistenceOverrides ?? Array.Empty<SceneEntityPlatformExistenceOverrideAsset>();
            HashSet<EditorOverrideScope> scopes = new HashSet<EditorOverrideScope>();
            for (int index = 0; index < overrideAssets.Length; index++) {
                SceneEntityPlatformExistenceOverrideAsset overrideAsset = overrideAssets[index];
                if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
                    continue;
                }

                EditorOverrideScope scope = new EditorOverrideScope(overrideAsset.PlatformId, overrideAsset.EnvironmentId);
                if (!scopes.Add(scope)) {
                    throw new InvalidOperationException($"Duplicate entity existence override scope '{scope}'.");
                }

                saveComponent.SetExistencePlatformOverride(scope, new SceneEntityPlatformExistenceOverrideAsset {
                    PlatformId = scope.PlatformId,
                    EnvironmentId = scope.EnvironmentId,
                    Exists = overrideAsset.Exists
                });
            }
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
            HashSet<EditorOverrideScope> scopes = new HashSet<EditorOverrideScope>();
            for (int index = 0; index < overrideAssets.Length; index++) {
                SceneEntityPlatformTransformOverrideAsset overrideAsset = overrideAssets[index];
                if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
                    continue;
                }

                EditorOverrideScope scope = new EditorOverrideScope(overrideAsset.PlatformId, overrideAsset.EnvironmentId);
                if (!scopes.Add(scope)) {
                    throw new InvalidOperationException($"Duplicate entity transform override scope '{scope}'.");
                }

                saveComponent.SetTransformPlatformOverride(scope, new SceneEntityPlatformTransformOverrideAsset {
                    PlatformId = scope.PlatformId,
                    EnvironmentId = scope.EnvironmentId,
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
            HashSet<EditorOverrideScope> scopes = new HashSet<EditorOverrideScope>();
            for (int platformIndex = 0; platformIndex < overrideAssets.Length; platformIndex++) {
                SceneEntityPlatformComponentOverrideAsset overrideAsset = overrideAssets[platformIndex];
                if (overrideAsset == null || string.IsNullOrWhiteSpace(overrideAsset.PlatformId)) {
                    continue;
                }

                EditorOverrideScope scope = new EditorOverrideScope(overrideAsset.PlatformId, overrideAsset.EnvironmentId);
                if (!scopes.Add(scope)) {
                    throw new InvalidOperationException($"Duplicate entity component override scope '{scope}'.");
                }

                EntityPlatformComponentOverrideState componentOverrideState = saveComponent.GetOrCreateComponentPlatformOverride(scope);
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
        /// Resolves the blueprint instance component attached to one scene-owned entity.
        /// </summary>
        /// <param name="entity">Entity whose blueprint instance component should be returned.</param>
        /// <returns>Attached blueprint instance component when present; otherwise null.</returns>
        BlueprintInstanceComponent FindBlueprintInstanceComponent(EditorEntity entity) {
            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is BlueprintInstanceComponent instanceComponent) {
                    return instanceComponent;
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


