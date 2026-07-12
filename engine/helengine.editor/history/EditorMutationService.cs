namespace helengine.editor {
    /// <summary>
    /// Records user-authored editor mutations into the undo/redo stack and emits tracked scene-dirty notifications.
    /// </summary>
    public class EditorMutationService {
        /// <summary>
        /// Undo/redo service that owns the recorded reversible operations.
        /// </summary>
        readonly IEditorUndoRedoService UndoRedoService;

        /// <summary>
        /// History capture service used to create detached entity and scene settings snapshots.
        /// </summary>
        readonly EditorHistoryCaptureService HistoryCaptureService;
        /// <summary>
        /// Registry that resolves component-specific history adapters for component-scoped inspector mutations.
        /// </summary>
        readonly ComponentHistoryAdapterRegistry HistoryAdapterRegistry;

        /// <summary>
        /// Session-owned callback that emits a tracked scene mutation notification.
        /// </summary>
        readonly Action MarkSceneMutated;

        /// <summary>
        /// Initializes one editor mutation recorder.
        /// </summary>
        /// <param name="undoRedoService">Undo/redo service that owns recorded operations.</param>
        /// <param name="historyCaptureService">History capture service used to snapshot live state.</param>
        /// <param name="historyAdapterRegistry">Registry that resolves component-specific history adapters.</param>
        /// <param name="markSceneMutated">Session-owned callback that emits a tracked scene-dirty notification.</param>
        public EditorMutationService(
            IEditorUndoRedoService undoRedoService,
            EditorHistoryCaptureService historyCaptureService,
            ComponentHistoryAdapterRegistry historyAdapterRegistry,
            Action markSceneMutated) {
            UndoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
            HistoryCaptureService = historyCaptureService ?? throw new ArgumentNullException(nameof(historyCaptureService));
            HistoryAdapterRegistry = historyAdapterRegistry ?? throw new ArgumentNullException(nameof(historyAdapterRegistry));
            MarkSceneMutated = markSceneMutated ?? throw new ArgumentNullException(nameof(markSceneMutated));
        }

        /// <summary>
        /// Captures one detached history snapshot for the supplied editor entity.
        /// </summary>
        /// <param name="entity">Editor entity that should be serialized into a detached snapshot.</param>
        /// <returns>Detached serialized entity snapshot.</returns>
        public SerializedEditorEntityState CaptureEntityState(EditorEntity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return HistoryCaptureService.CaptureEntity(entity);
        }

        /// <summary>
        /// Records one authored entity creation.
        /// </summary>
        /// <param name="entity">Created live editor entity.</param>
        /// <param name="previousSelectionEntityId">Stable scene entity id selected before creation, or zero when none existed.</param>
        public void RecordCreatedEntity(EditorEntity entity, uint previousSelectionEntityId) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            UndoRedoService.Record(new EntityCreationHistoryOperation(
                HistoryCaptureService.CaptureEntity(entity),
                previousSelectionEntityId));
            MarkSceneMutated();
        }

        /// <summary>
        /// Records one authored entity reparent mutation.
        /// </summary>
        /// <param name="entity">Reparented live editor entity.</param>
        /// <param name="previousParent">Original parent entity, or null when the entity originally belonged at the scene root.</param>
        /// <param name="currentParent">Destination parent entity, or null when the entity now belongs at the scene root.</param>
        public void RecordEntityReparent(EditorEntity entity, Entity previousParent, Entity currentParent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            uint entityId = GetRequiredSceneEntityId(entity);
            uint previousParentEntityId = GetSceneEntityId(previousParent);
            uint currentParentEntityId = GetSceneEntityId(currentParent);
            UndoRedoService.Record(new EntityReparentHistoryOperation(entityId, previousParentEntityId, currentParentEntityId));
            MarkSceneMutated();
        }

        /// <summary>
        /// Records one authored scene settings mutation.
        /// </summary>
        /// <param name="previousSceneSettings">Detached snapshot of the prior scene settings.</param>
        /// <param name="currentSceneSettings">Scene settings that were applied to the live scene.</param>
        public void RecordSceneSettingsChange(SceneSettingsAsset previousSceneSettings, SceneSettingsAsset currentSceneSettings) {
            if (previousSceneSettings == null) {
                throw new ArgumentNullException(nameof(previousSceneSettings));
            }
            if (currentSceneSettings == null) {
                throw new ArgumentNullException(nameof(currentSceneSettings));
            }

            UndoRedoService.Record(new SceneSettingsHistoryOperation(
                HistoryCaptureService.CloneSceneSettings(previousSceneSettings),
                HistoryCaptureService.CloneSceneSettings(currentSceneSettings)));
            MarkSceneMutated();
        }

        /// <summary>
        /// Records one entity-scoped mutation using detached before/after snapshots of the supplied live entity.
        /// </summary>
        /// <param name="entity">Live editor entity that was mutated.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        public void RecordEntityStateChange(EditorEntity entity, SerializedEditorEntityState previousEntityState) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (previousEntityState == null) {
                throw new ArgumentNullException(nameof(previousEntityState));
            }

            UndoRedoService.Record(new EntityStateChangeHistoryOperation(
                previousEntityState,
                HistoryCaptureService.CaptureEntity(entity)));
            MarkSceneMutated();
        }

        /// <summary>
        /// Records one component-scoped mutation using the adapter registered for the supplied component type.
        /// </summary>
        /// <param name="entity">Live editor entity that owns the mutated component.</param>
        /// <param name="component">Component whose mutation should be recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        public void RecordComponentMutation(EditorEntity entity, Component component, SerializedEditorEntityState previousEntityState) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (previousEntityState == null) {
                throw new ArgumentNullException(nameof(previousEntityState));
            }

            SerializedEditorEntityState currentEntityState = HistoryCaptureService.CaptureEntity(entity);
            IComponentHistoryAdapter historyAdapter = HistoryAdapterRegistry.Resolve(component);
            UndoRedoService.Record(historyAdapter.CreateOperation(component, previousEntityState, currentEntityState));
            MarkSceneMutated();
        }

        /// <summary>
        /// Returns the stable scene entity id for the supplied entity and throws when none exists.
        /// </summary>
        /// <param name="entity">Entity whose stable scene id should be resolved.</param>
        /// <returns>Stable non-zero scene entity id.</returns>
        uint GetRequiredSceneEntityId(Entity entity) {
            uint entityId = GetSceneEntityId(entity);
            if (entityId == 0u) {
                throw new InvalidOperationException("History recording requires a non-zero stable scene entity id.");
            }

            return entityId;
        }

        /// <summary>
        /// Returns the stable scene entity id for the supplied entity, or zero when no authored scene id exists.
        /// </summary>
        /// <param name="entity">Entity whose stable scene id should be resolved.</param>
        /// <returns>Stable scene entity id, or zero when none exists.</returns>
        uint GetSceneEntityId(Entity entity) {
            if (entity == null || entity.Components == null) {
                return 0u;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is EntitySaveComponent saveComponent) {
                    return saveComponent.EntityId;
                }
            }

            return 0u;
        }
    }
}
