namespace helengine.editor {
    /// <summary>
    /// Bridges editor-global tools to the active editor session's entity history recording callbacks.
    /// </summary>
    public static class EditorEntityHistoryMutationService {
        /// <summary>
        /// Callback used to capture one detached history snapshot for a live editor entity.
        /// </summary>
        public static Func<EditorEntity, SerializedEditorEntityState> CaptureEntityState { get; set; }

        /// <summary>
        /// Callback used to record one entity-scoped mutation from a detached pre-mutation snapshot.
        /// </summary>
        public static Action<EditorEntity, SerializedEditorEntityState> RecordEntityStateChange { get; set; }

        /// <summary>
        /// Attempts to capture one detached history snapshot for the supplied live entity.
        /// </summary>
        /// <param name="entity">Live entity that may participate in editor history.</param>
        /// <param name="entityState">Captured history snapshot when available.</param>
        /// <returns>True when the entity snapshot was captured; otherwise false.</returns>
        public static bool TryCaptureEntityState(Entity entity, out SerializedEditorEntityState entityState) {
            entityState = null;
            if (entity is not EditorEntity editorEntity || editorEntity.IsDisposed || CaptureEntityState == null) {
                return false;
            }

            entityState = CaptureEntityState(editorEntity);
            return entityState != null;
        }

        /// <summary>
        /// Attempts to record one entity-scoped mutation for the supplied live entity using the detached pre-mutation snapshot.
        /// </summary>
        /// <param name="entity">Live entity that was mutated.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <returns>True when the mutation was recorded into undo/redo history; otherwise false.</returns>
        public static bool TryRecordEntityStateChange(Entity entity, SerializedEditorEntityState previousEntityState) {
            if (previousEntityState == null || entity is not EditorEntity editorEntity || editorEntity.IsDisposed || RecordEntityStateChange == null) {
                return false;
            }

            RecordEntityStateChange(editorEntity, previousEntityState);
            return true;
        }

        /// <summary>
        /// Clears the active editor-session callbacks.
        /// </summary>
        public static void Reset() {
            CaptureEntityState = null;
            RecordEntityStateChange = null;
        }
    }
}
