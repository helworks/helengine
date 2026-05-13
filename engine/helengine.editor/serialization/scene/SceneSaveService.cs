namespace helengine.editor {
    /// <summary>
    /// Serializes the current editor scene into one `.helen` asset stored under the project assets folder.
    /// </summary>
    public class SceneSaveService {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project assets root.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Registry used to serialize supported component types.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;

        /// <summary>
        /// Tracks stable entity ids for the current save session.
        /// </summary>
        readonly SceneEntityReferenceTable EntityReferenceTable;

        /// <summary>
        /// Service that wraps component payloads with editor-only platform override metadata.
        /// </summary>
        readonly ComponentPlatformOverridePayloadService OverridePayloadService;
        /// <summary>
        /// Service that infers stable scene asset references from live runtime component assignments during save.
        /// </summary>
        readonly SceneAssetReferenceInferenceService AssetReferenceInferenceService;
        /// <summary>
        /// Service that resolves common entity transforms separately from projected platform overrides during serialization.
        /// </summary>
        readonly EntityPlatformTransformEditingService TransformEditingService;
        /// <summary>
        /// Service that owns stable component keys and entity-level platform component existence overrides.
        /// </summary>
        readonly ComponentPlatformEditingService ComponentEditingService;

        /// <summary>
        /// Initializes a new scene save service for one project root.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to serialize persisted components.</param>
        public SceneSaveService(string projectRootPath, ComponentPersistenceRegistry persistenceRegistry) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            PersistenceRegistry = persistenceRegistry;
            EntityReferenceTable = new SceneEntityReferenceTable();
            OverridePayloadService = new ComponentPlatformOverridePayloadService();
            AssetReferenceInferenceService = new SceneAssetReferenceInferenceService(ProjectRootPath);
            TransformEditingService = new EntityPlatformTransformEditingService();
            ComponentEditingService = new ComponentPlatformEditingService();
        }

        /// <summary>
        /// Saves the current editor scene to one `.helen` file on disk.
        /// </summary>
        /// <param name="fullPath">Absolute path where the scene file should be written.</param>
        public void Save(string fullPath) {
            Save(fullPath, new SceneSettingsAsset());
        }

        /// <summary>
        /// Saves the current editor scene to one `.helen` file on disk using the supplied scene-level settings.
        /// </summary>
        /// <param name="fullPath">Absolute path where the scene file should be written.</param>
        /// <param name="sceneSettings">Scene-level settings that should be persisted with the scene.</param>
        public void Save(string fullPath, SceneSettingsAsset sceneSettings) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }
            if (sceneSettings.CanvasProfile == null) {
                throw new InvalidOperationException("Scene settings must include a canvas profile.");
            }

            SceneAsset asset = BuildSceneAsset(fullPath, sceneSettings);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Scene path does not include a writable directory.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, asset);
        }

        /// <summary>
        /// Builds a scene asset payload for the current editor scene.
        /// </summary>
        /// <param name="fullPath">Absolute path where the scene will be stored.</param>
        /// <param name="sceneSettings">Scene-level settings that should be persisted with the scene.</param>
        /// <returns>Serialized scene asset payload.</returns>
        SceneAsset BuildSceneAsset(string fullPath, SceneSettingsAsset sceneSettings) {
            string sceneId = BuildSceneId(fullPath);
            List<SceneEntityAsset> rootEntities = new List<SceneEntityAsset>();
            List<SceneAssetReference> assetReferences = new List<SceneAssetReference>();
            HashSet<string> assetReferenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int i = 0; i < entities.Count; i++) {
                if (entities[i] is not EditorEntity editorEntity) {
                    continue;
                }
                if (editorEntity.Parent != null) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }
                if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                    continue;
                }

                rootEntities.Add(SerializeEntity(editorEntity, assetReferences, assetReferenceKeys));
            }

            return new SceneAsset {
                Id = sceneId,
                RootEntities = rootEntities.ToArray(),
                AssetReferences = assetReferences.ToArray(),
                SceneSettings = CloneSceneSettings(sceneSettings)
            };
        }

        /// <summary>
        /// Serializes one editor entity recursively into a scene entity asset.
        /// </summary>
        /// <param name="entity">Editor entity to serialize.</param>
        /// <returns>Serialized scene entity asset.</returns>
        SceneEntityAsset SerializeEntity(
            EditorEntity entity,
            List<SceneAssetReference> assetReferences,
            HashSet<string> assetReferenceKeys) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (assetReferences == null) {
                throw new ArgumentNullException(nameof(assetReferences));
            }
            if (assetReferenceKeys == null) {
                throw new ArgumentNullException(nameof(assetReferenceKeys));
            }

            List<SceneComponentAssetRecord> componentRecords = new List<SceneComponentAssetRecord>();
            EntitySaveComponent saveComponent = FindEntitySaveComponent(entity);
            if (saveComponent != null) {
                TransformEditingService.PersistActivePlatform(entity, saveComponent);
            }

            string entityId = EntityReferenceTable.GetOrCreateEntityId(entity);
            int persistedComponentIndex = 0;
            if (entity.Components != null) {
                for (int i = 0; i < entity.Components.Count; i++) {
                    Component component = entity.Components[i];
                    if (component == null || component is IEditorHiddenComponent) {
                        continue;
                    }

                    EntityComponentSaveState saveState = null;
                    if (saveComponent != null) {
                        saveState = saveComponent.GetOrCreateComponentState(component);
                        saveState.ComponentKey = ComponentEditingService.EnsureComponentKey(component, saveComponent);
                        AssetReferenceInferenceService.PopulateAssetReferences(component, saveState);
                    }

                    IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(component);
                    SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(component, persistedComponentIndex, saveState);
                    if (saveState != null) {
                        baseRecord.ComponentKey = saveState.ComponentKey;
                    }
                    componentRecords.Add(OverridePayloadService.Wrap(baseRecord, saveState));
                    AppendAssetReferences(saveState, assetReferences, assetReferenceKeys);
                    persistedComponentIndex++;
                }
            }

            AppendPlatformComponentOverrideAssetReferences(saveComponent, assetReferences, assetReferenceKeys);

            List<SceneEntityAsset> childEntities = new List<SceneEntityAsset>();
            if (entity.Children != null) {
                for (int i = 0; i < entity.Children.Count; i++) {
                    if (entity.Children[i] is not EditorEntity childEntity) {
                        continue;
                    }
                    if (childEntity.InternalEntity) {
                        continue;
                    }
                    if (childEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                        continue;
                    }

                    childEntities.Add(SerializeEntity(childEntity, assetReferences, assetReferenceKeys));
                }
            }

            return new SceneEntityAsset {
                Id = entityId,
                Name = entity.Name,
                LocalPosition = ResolveSerializedLocalPosition(entity, saveComponent),
                LocalScale = ResolveSerializedLocalScale(entity, saveComponent),
                LocalOrientation = ResolveSerializedLocalOrientation(entity, saveComponent),
                Components = componentRecords.ToArray(),
                PlatformTransformOverrides = ClonePlatformTransformOverrides(saveComponent),
                PlatformComponentOverrides = ClonePlatformComponentOverrides(saveComponent),
                Children = childEntities.ToArray()
            };
        }

        /// <summary>
        /// Resolves the common local position that should be serialized for one entity.
        /// </summary>
        /// <param name="entity">Entity whose local position should be serialized.</param>
        /// <param name="saveComponent">Hidden save component that may hold a projected common transform snapshot.</param>
        /// <returns>Common local position to serialize.</returns>
        float3 ResolveSerializedLocalPosition(EditorEntity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (saveComponent == null) {
                return entity.LocalPosition;
            }

            return TransformEditingService.ResolveSerializedLocalPosition(entity, saveComponent);
        }

        /// <summary>
        /// Resolves the common local scale that should be serialized for one entity.
        /// </summary>
        /// <param name="entity">Entity whose local scale should be serialized.</param>
        /// <param name="saveComponent">Hidden save component that may hold a projected common transform snapshot.</param>
        /// <returns>Common local scale to serialize.</returns>
        float3 ResolveSerializedLocalScale(EditorEntity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (saveComponent == null) {
                return entity.LocalScale;
            }

            return TransformEditingService.ResolveSerializedLocalScale(entity, saveComponent);
        }

        /// <summary>
        /// Resolves the common local orientation that should be serialized for one entity.
        /// </summary>
        /// <param name="entity">Entity whose local orientation should be serialized.</param>
        /// <param name="saveComponent">Hidden save component that may hold a projected common transform snapshot.</param>
        /// <returns>Common local orientation to serialize.</returns>
        float4 ResolveSerializedLocalOrientation(EditorEntity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (saveComponent == null) {
                return entity.LocalOrientation;
            }

            return TransformEditingService.ResolveSerializedLocalOrientation(entity, saveComponent);
        }

        /// <summary>
        /// Clones the entity transform overrides stored on one hidden save component into serializable scene asset payloads.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <returns>Cloned scene-asset transform override payloads.</returns>
        SceneEntityPlatformTransformOverrideAsset[] ClonePlatformTransformOverrides(EntitySaveComponent saveComponent) {
            if (saveComponent == null) {
                return Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            }

            List<SceneEntityPlatformTransformOverrideAsset> overrideAssets = new List<SceneEntityPlatformTransformOverrideAsset>();
            foreach (SceneEntityPlatformTransformOverrideAsset overrideState in saveComponent.EnumerateTransformPlatformOverrides()) {
                if (overrideState == null) {
                    continue;
                }

                overrideAssets.Add(new SceneEntityPlatformTransformOverrideAsset {
                    PlatformId = overrideState.PlatformId,
                    HasLocalPositionOverride = overrideState.HasLocalPositionOverride,
                    LocalPosition = overrideState.LocalPosition,
                    HasLocalScaleOverride = overrideState.HasLocalScaleOverride,
                    LocalScale = overrideState.LocalScale,
                    HasLocalOrientationOverride = overrideState.HasLocalOrientationOverride,
                    LocalOrientation = overrideState.LocalOrientation
                });
            }

            return overrideAssets.ToArray();
        }

        /// <summary>
        /// Clones the entity component existence overrides stored on one hidden save component into serializable scene asset payloads.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the component existence override metadata.</param>
        /// <returns>Cloned scene-asset component existence override payloads.</returns>
        SceneEntityPlatformComponentOverrideAsset[] ClonePlatformComponentOverrides(EntitySaveComponent saveComponent) {
            if (saveComponent == null) {
                return Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
            }

            List<SceneEntityPlatformComponentOverrideAsset> overrideAssets = new List<SceneEntityPlatformComponentOverrideAsset>();
            foreach (EntityPlatformComponentOverrideState overrideState in saveComponent.EnumerateComponentPlatformOverrides()) {
                if (overrideState == null || !overrideState.HasAnyOverrides) {
                    continue;
                }

                List<string> removedComponentKeys = new List<string>();
                foreach (string componentKey in overrideState.EnumerateRemovedComponentKeys()) {
                    if (!string.IsNullOrWhiteSpace(componentKey)) {
                        removedComponentKeys.Add(componentKey);
                    }
                }

                List<SceneEntityPlatformAddedComponentAsset> addedComponents = new List<SceneEntityPlatformAddedComponentAsset>();
                foreach (EntityPlatformAddedComponentState addedComponentState in overrideState.EnumerateAddedComponents()) {
                    SceneEntityPlatformAddedComponentAsset addedComponentAsset = SerializeAddedComponent(addedComponentState);
                    if (addedComponentAsset != null) {
                        addedComponents.Add(addedComponentAsset);
                    }
                }

                overrideAssets.Add(new SceneEntityPlatformComponentOverrideAsset {
                    PlatformId = overrideState.PlatformId,
                    RemovedComponentKeys = removedComponentKeys.ToArray(),
                    AddedComponents = addedComponents.ToArray()
                });
            }

            return overrideAssets.ToArray();
        }

        /// <summary>
        /// Serializes one detached platform-only component state into the scene payload used by entity-level component existence overrides.
        /// </summary>
        /// <param name="addedComponentState">Detached platform-only component state to serialize.</param>
        /// <returns>Serialized platform-only component payload, or null when the detached state is incomplete.</returns>
        SceneEntityPlatformAddedComponentAsset SerializeAddedComponent(EntityPlatformAddedComponentState addedComponentState) {
            if (addedComponentState == null || addedComponentState.Component == null || addedComponentState.SaveState == null) {
                return null;
            }

            IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(addedComponentState.Component);
            SceneComponentAssetRecord componentRecord = descriptor.SerializeComponent(addedComponentState.Component, 0, addedComponentState.SaveState);
            componentRecord.ComponentKey = addedComponentState.ComponentKey;
            return new SceneEntityPlatformAddedComponentAsset {
                Component = componentRecord
            };
        }

        /// <summary>
        /// Appends one component save-state's asset references to the scene dependency list.
        /// </summary>
        /// <param name="saveState">Component save-state containing asset references.</param>
        /// <param name="assetReferences">Scene-level dependency list being populated.</param>
        /// <param name="assetReferenceKeys">Deduplication keys for already-queued references.</param>
        void AppendAssetReferences(
            EntityComponentSaveState saveState,
            List<SceneAssetReference> assetReferences,
            HashSet<string> assetReferenceKeys) {
            if (saveState == null) {
                return;
            }

            foreach (SceneAssetReference reference in saveState.EnumerateAssetReferences()) {
                if (reference == null) {
                    continue;
                }

                string referenceKey = BuildAssetReferenceKey(reference);
                if (assetReferenceKeys.Add(referenceKey)) {
                    assetReferences.Add(reference);
                }
            }

            foreach (EntityComponentPlatformOverrideState overrideState in saveState.EnumeratePlatformOverrides()) {
                AppendPlatformOverrideAssetReferences(overrideState, assetReferences, assetReferenceKeys);
            }

        }

        /// <summary>
        /// Appends asset references used by detached platform-only components into the scene dependency list.
        /// </summary>
        /// <param name="saveComponent">Hidden entity save component that owns the platform-only component metadata.</param>
        /// <param name="assetReferences">Scene-level dependency list being populated.</param>
        /// <param name="assetReferenceKeys">Deduplication keys for already-queued references.</param>
        void AppendPlatformComponentOverrideAssetReferences(
            EntitySaveComponent saveComponent,
            List<SceneAssetReference> assetReferences,
            HashSet<string> assetReferenceKeys) {
            if (saveComponent == null) {
                return;
            }

            foreach (EntityPlatformComponentOverrideState componentOverrideState in saveComponent.EnumerateComponentPlatformOverrides()) {
                foreach (EntityPlatformAddedComponentState addedComponentState in componentOverrideState.EnumerateAddedComponents()) {
                    if (addedComponentState?.SaveState != null) {
                        AppendAssetReferences(addedComponentState.SaveState, assetReferences, assetReferenceKeys);
                    }
                }
            }
        }

        /// <summary>
        /// Appends one platform override payload's asset references to the scene dependency list.
        /// </summary>
        /// <param name="overrideState">Platform override payload whose asset references should be appended.</param>
        /// <param name="assetReferences">Scene-level dependency list being populated.</param>
        /// <param name="assetReferenceKeys">Deduplication keys for already-queued references.</param>
        void AppendPlatformOverrideAssetReferences(
            EntityComponentPlatformOverrideState overrideState,
            List<SceneAssetReference> assetReferences,
            HashSet<string> assetReferenceKeys) {
            if (overrideState == null) {
                return;
            }

            foreach (SceneAssetReference reference in overrideState.EnumerateAssetReferences()) {
                if (reference == null) {
                    continue;
                }

                string referenceKey = BuildAssetReferenceKey(reference);
                if (assetReferenceKeys.Add(referenceKey)) {
                    assetReferences.Add(reference);
                }
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

        /// <summary>
        /// Builds the project-relative scene asset id for one output file path.
        /// </summary>
        /// <param name="fullPath">Absolute file path where the scene will be stored.</param>
        /// <returns>Project-relative scene asset id stored inside the scene file.</returns>
        string BuildSceneId(string fullPath) {
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                throw new InvalidOperationException("Scene files must be stored inside the project assets folder.");
            }

            return Path.GetRelativePath(AssetsRootPath, normalizedPath).Replace('\\', '/');
        }

        /// <summary>
        /// Determines whether one full path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }

            if (string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator = AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? AssetsRootPath
                : AssetsRootPath + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a stable deduplication key for one scene asset reference.
        /// </summary>
        /// <param name="reference">Scene asset reference to key.</param>
        /// <returns>Stable deduplication key.</returns>
        static string BuildAssetReferenceKey(SceneAssetReference reference) {
            return string.Concat(
                reference.SourceKind.ToString(),
                "|",
                reference.RelativePath ?? string.Empty,
                "|",
                reference.ProviderId ?? string.Empty,
                "|",
                reference.AssetId ?? string.Empty);
        }

        /// <summary>
        /// Creates a detached copy of one scene settings payload before it is serialized.
        /// </summary>
        /// <param name="sceneSettings">Scene settings that should be copied.</param>
        /// <returns>Detached scene settings copy.</returns>
        static SceneSettingsAsset CloneSceneSettings(SceneSettingsAsset sceneSettings) {
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }
            if (sceneSettings.CanvasProfile == null) {
                throw new InvalidOperationException("Scene settings must include a canvas profile.");
            }

            return new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = sceneSettings.CanvasProfile.Width,
                    Height = sceneSettings.CanvasProfile.Height
                }
            };
        }
    }
}
