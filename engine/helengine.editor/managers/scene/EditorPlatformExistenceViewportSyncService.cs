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
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one platform-existence viewport sync service for a project.
        /// </summary>
        public EditorPlatformExistenceViewportSyncService(ObjectManager objectManager, string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ExistenceService = new EntityPlatformExistenceEditingService();
            ObjectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            ProjectRootPath = projectRootPath;
        }

        /// <summary>
        /// Applies runtime suppression for every authored scene entity against the active platform's scope path.
        /// Group settings are re-read on each call because the call is event-driven and the file is small.
        /// </summary>
        public void Apply(string activePlatformId) {
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                return;
            }

            EditorOverrideScopeResolver resolver = EditorOverrideScopeResolver.Load(ProjectRootPath);
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

                EditorOverrideScope target = resolver.BuildTargetPath(resolver.ResolveLevelOrder(saveComponent.OverrideLevelOrder), activePlatformId, string.Empty);
                editorEntity.RuntimeSuppressed = !ExistenceService.ResolveExists(saveComponent, target);
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
