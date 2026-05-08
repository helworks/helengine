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
                        saveComponent.TryGetComponentState(component, out saveState);
                    }

                    IComponentPersistenceDescriptor descriptor = PersistenceRegistry.GetDescriptor(component);
                    SceneComponentAssetRecord baseRecord = descriptor.SerializeComponent(component, persistedComponentIndex, saveState);
                    componentRecords.Add(OverridePayloadService.Wrap(baseRecord, saveState));
                    AppendAssetReferences(saveState, assetReferences, assetReferenceKeys);
                    persistedComponentIndex++;
                }
            }

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
                LocalPosition = entity.LocalPosition,
                LocalScale = entity.LocalScale,
                LocalOrientation = entity.LocalOrientation,
                Components = componentRecords.ToArray(),
                Children = childEntities.ToArray()
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
