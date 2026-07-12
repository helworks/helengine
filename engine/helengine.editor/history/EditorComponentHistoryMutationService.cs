namespace helengine.editor {
    /// <summary>
    /// Provides one static bridge custom editor tools can use to capture and record component-scoped undo history without reaching into the live editor session.
    /// </summary>
    public static class EditorComponentHistoryMutationService {
        /// <summary>
        /// Gets or sets the session-owned callback that captures one detached entity snapshot for the supplied editor entity.
        /// </summary>
        public static Func<EditorEntity, SerializedEditorEntityState> CaptureEntityState { get; set; }

        /// <summary>
        /// Gets or sets the session-owned callback that records one component mutation using the supplied previous entity snapshot.
        /// </summary>
        public static Action<EditorEntity, Component, SerializedEditorEntityState> RecordComponentMutation { get; set; }

        /// <summary>
        /// Attempts to capture one detached entity snapshot for the entity that owns the supplied component.
        /// </summary>
        /// <param name="component">Component whose owning entity should be captured.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation when successful.</param>
        /// <returns>True when the entity snapshot was captured; otherwise false.</returns>
        public static bool TryCaptureEntityState(Component component, out SerializedEditorEntityState previousEntityState) {
            if (component == null || component.Entity is not EditorEntity editorEntity || editorEntity.IsDisposed || CaptureEntityState == null) {
                previousEntityState = null;
                return false;
            }

            previousEntityState = CaptureEntityState(editorEntity);
            return true;
        }

        /// <summary>
        /// Attempts to record one component-scoped mutation for the supplied live component.
        /// </summary>
        /// <param name="component">Live component whose mutation should be recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <returns>True when the mutation was recorded through the active editor session; otherwise false.</returns>
        public static bool TryRecordComponentMutation(Component component, SerializedEditorEntityState previousEntityState) {
            if (previousEntityState == null
                || component == null
                || component.Entity is not EditorEntity editorEntity
                || editorEntity.IsDisposed
                || RecordComponentMutation == null) {
                return false;
            }

            RecordComponentMutation(editorEntity, component, previousEntityState);
            return true;
        }

        /// <summary>
        /// Clears the current editor-session callbacks.
        /// </summary>
        public static void Reset() {
            CaptureEntityState = null;
            RecordComponentMutation = null;
        }
    }
}
