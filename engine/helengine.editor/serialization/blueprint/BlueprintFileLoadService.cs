namespace helengine.editor {
    /// <summary>
    /// Reads one `.hblueprint` file from disk and materializes its editable root entity.
    /// </summary>
    public class BlueprintFileLoadService {
        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Blueprint-load service that reconstructs editor entities from blueprint assets.
        /// </summary>
        readonly BlueprintLoadService BlueprintLoadService;

        /// <summary>
        /// Initializes a new blueprint-file load service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime-backed assets.</param>
        public BlueprintFileLoadService(
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
            BlueprintLoadService = new BlueprintLoadService(persistenceRegistry, referenceResolver);
        }

        /// <summary>
        /// Loads one `.hblueprint` blueprint file from disk.
        /// </summary>
        /// <param name="fullPath">Absolute path to the blueprint file.</param>
        /// <returns>Loaded editor blueprint document.</returns>
        public LoadedEditorBlueprintDocument Load(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Blueprint path must be provided.", nameof(fullPath));
            }

            string normalizedPath = Path.GetFullPath(fullPath);
            HashSet<Entity> existingEntities = new HashSet<Entity>(Core.Instance.ObjectManager.Entities);
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                if (!normalizedPath.StartsWith(ProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("Blueprint path must be inside the current project.");
                }

                EngineBinaryReadContext.CurrentAssetPath = normalizedPath;
                using FileStream stream = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Asset deserializedAsset = AssetSerializer.Deserialize(stream);
                if (deserializedAsset is not BlueprintAsset blueprintAsset) {
                    throw new InvalidOperationException("Blueprint file did not deserialize into a BlueprintAsset.");
                }

                LoadedEditorBlueprintDocument loadedDocument = BlueprintLoadService.Load(blueprintAsset);
                if (loadedDocument.RootEntity == null) {
                    throw new InvalidOperationException("Blueprint load did not materialize a root entity.");
                }

                loadedDocument.RootEntity.Enabled = false;
                return loadedDocument;
            } catch (Exception ex) {
                CleanupFailedLoad(existingEntities);
                throw new InvalidOperationException($"Blueprint load failed: {ex.Message}", ex);
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Removes entities created during a failed blueprint load attempt.
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
