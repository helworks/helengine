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
        /// Settings error messages already reported, so one broken settings file does not flood the log on every event.
        /// </summary>
        readonly HashSet<string> ReportedSettingsErrors;

        /// <summary>
        /// Raised once per distinct message when the project's platform settings cannot be resolved. Suppression is
        /// left untouched for that call, so the viewport keeps showing what it last resolved.
        /// </summary>
        public event Action<string> SettingsError;

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
            ReportedSettingsErrors = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Applies runtime suppression for every authored scene entity against the active platform's scope path.
        /// Group settings are re-read on each call because the call is event-driven and the file is small.
        /// Inconsistent or unreadable settings are reported once per distinct message and leave suppression unchanged.
        /// </summary>
        public void Apply(string activePlatformId) {
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                return;
            }

            if (!EditorOverrideScopeResolver.TryLoad(ProjectRootPath, out EditorOverrideScopeResolver resolver, out string error)) {
                ReportSettingsError(error);
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

                EditorOverrideScope target = resolver.BuildTargetPath(resolver.ResolveLevelOrder(saveComponent.OverrideLevelOrder), activePlatformId, string.Empty);
                editorEntity.RuntimeSuppressed = !ExistenceService.ResolveExists(saveComponent, target);
            }
        }

        /// <summary>
        /// Reports one settings failure the first time that exact message is seen.
        /// </summary>
        /// <param name="error">Message naming the offending settings entry.</param>
        void ReportSettingsError(string error) {
            if (string.IsNullOrWhiteSpace(error) || !ReportedSettingsErrors.Add(error)) {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Platform existence viewport sync could not read the project platform settings: {error}");
            SettingsError?.Invoke(error);
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
