namespace helengine.editor {
    /// <summary>
    /// Reads one `.helen` file from disk and materializes editor entities from it.
    /// </summary>
    public class SceneFileLoadService {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;
        /// <summary>
        /// Scene-load service that reconstructs entities from scene assets.
        /// </summary>
        readonly SceneLoadService SceneLoadService;

        /// <summary>
        /// Initializes a new scene-file load service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime-backed assets.</param>
        public SceneFileLoadService(
            string projectRootPath,
            ComponentPersistenceRegistry persistenceRegistry,
            ISceneAssetReferenceResolver referenceResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            SceneLoadService = new SceneLoadService(persistenceRegistry, referenceResolver);
        }

        /// <summary>
        /// Loads one `.helen` scene file from disk.
        /// </summary>
        /// <param name="fullPath">Absolute path to the scene file.</param>
        /// <returns>Loaded editor scene document.</returns>
        public LoadedEditorSceneDocument Load(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            string normalizedPath = Path.GetFullPath(fullPath);
            HashSet<Entity> existingEntities = new HashSet<Entity>(Core.Instance.ObjectManager.Entities);
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                if (!normalizedPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("Scene path must be inside the current project.");
                }

                EngineBinaryReadContext.CurrentAssetPath = normalizedPath;
                using FileStream stream = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Asset deserializedAsset = AssetSerializer.Deserialize(stream);
                if (deserializedAsset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException("Scene file did not deserialize into a SceneAsset.");
                }

                IReadOnlyList<EditorEntity> loadedRoots = SceneLoadService.Load(sceneAsset);
                EditorEntity[] rootEntityArray = loadedRoots.ToArray();
                SetRootsEnabled(rootEntityArray, false);
                return new LoadedEditorSceneDocument {
                    RootEntities = rootEntityArray,
                    SceneSettings = sceneAsset.SceneSettings
                };
            } catch (Exception ex) {
                CleanupFailedLoad(existingEntities);
                throw new InvalidOperationException($"Scene load failed: {ex.Message}", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Applies the enabled state to each loaded root entity.
        /// </summary>
        /// <param name="roots">Loaded root entities to update.</param>
        /// <param name="enabled">Enabled state applied to every root.</param>
        void SetRootsEnabled(IReadOnlyList<EditorEntity> roots, bool enabled) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            for (int i = 0; i < roots.Count; i++) {
                if (roots[i] == null) {
                    throw new InvalidOperationException("Loaded scene contained a null root entity.");
                }

                roots[i].Enabled = enabled;
            }
        }

        /// <summary>
        /// Removes entities created during a failed scene load attempt.
        /// </summary>
        /// <param name="existingEntities">Entities that existed before load started.</param>
        void CleanupFailedLoad(HashSet<Entity> existingEntities) {
            if (existingEntities == null) {
                throw new ArgumentNullException(nameof(existingEntities));
            }

            List<Entity> liveEntities = new List<Entity>(Core.Instance.ObjectManager.Entities);
            for (int i = 0; i < liveEntities.Count; i++) {
                Entity entity = liveEntities[i];
                if (existingEntities.Contains(entity)) {
                    continue;
                }
                if (entity is not EditorEntity editorEntity) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }

                editorEntity.Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(editorEntity);
            }
        }
    }
}
