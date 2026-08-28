namespace helengine.editor {
    /// <summary>
    /// Suppresses authored scene entities that do not exist on the active project platform so the viewport renders only
    /// active-platform content, while the scene hierarchy keeps listing every entity for multiplatform editing.
    /// Event-driven: invoked on scene load, active-platform changes, existence-override edits, and history replay.
    /// </summary>
    public sealed class EditorPlatformExistenceViewportSyncService {
        /// <summary>
        /// Service that resolves per-platform entity existence overrides.
        /// </summary>
        readonly EntityPlatformExistenceEditingService ExistenceService;
        readonly ObjectManager ObjectManager;

        /// <summary>
        /// Initializes one platform-existence viewport sync service.
        /// </summary>
        public EditorPlatformExistenceViewportSyncService(ObjectManager objectManager) {
            ExistenceService = new EntityPlatformExistenceEditingService();
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
        }

        /// <summary>
        /// Applies runtime suppression for every authored scene entity based on the supplied active platform.
        /// </summary>
        /// <param name="activePlatformId">Active project platform id; blank ids apply nothing.</param>
        public void Apply(string activePlatformId) {
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                return;
            }

            List<Entity> entities = ObjectManager.Entities;
            for (int index = 0; index < entities.Count; index++) {
                if (entities[index] is not EditorEntity editorEntity
                    || editorEntity.IsDisposed
                    || !editorEntity.IsSceneOwned
                    || editorEntity.InternalEntity) {
                    continue;
                }

                EntitySaveComponent saveComponent = FindSaveComponent(editorEntity);
                if (saveComponent == null) {
                    continue;
                }

                editorEntity.RuntimeSuppressed = !ExistenceService.ResolveExists(saveComponent, activePlatformId);
            }
        }

        /// <summary>
        /// Finds the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached save component, or null when absent.</returns>
        static EntitySaveComponent FindSaveComponent(EditorEntity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }
    }
}
