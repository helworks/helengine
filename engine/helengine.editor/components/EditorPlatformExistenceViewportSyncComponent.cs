namespace helengine.editor {
    /// <summary>
    /// Suppresses authored scene entities that do not exist on the active project platform so the viewport renders only
    /// active-platform content, while the scene hierarchy keeps listing every entity for multiplatform editing.
    /// </summary>
    public class EditorPlatformExistenceViewportSyncComponent : UpdateComponent {
        /// <summary>
        /// Service that resolves per-platform entity existence overrides.
        /// </summary>
        readonly EntityPlatformExistenceEditingService ExistenceService;

        /// <summary>
        /// Resolver returning the active project platform id.
        /// </summary>
        readonly Func<string> ActivePlatformResolver;

        /// <summary>
        /// Initializes one platform-existence viewport sync component.
        /// </summary>
        /// <param name="activePlatformResolver">Resolver returning the active project platform id.</param>
        public EditorPlatformExistenceViewportSyncComponent(Func<string> activePlatformResolver) {
            ActivePlatformResolver = activePlatformResolver ?? throw new ArgumentNullException(nameof(activePlatformResolver));
            ExistenceService = new EntityPlatformExistenceEditingService();
        }

        /// <summary>
        /// Applies runtime suppression for every authored scene entity based on the active platform's existence state.
        /// </summary>
        public override void Update() {
            string activePlatformId = ActivePlatformResolver();
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                return;
            }

            List<Entity> entities = Core.Instance.ObjectManager.Entities;
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
