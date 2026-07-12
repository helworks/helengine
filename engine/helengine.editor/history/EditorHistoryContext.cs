namespace helengine.editor {
    /// <summary>
    /// Aggregates the editor-session callbacks required by undo/redo operations to inspect and mutate live scene state.
    /// </summary>
    public class EditorHistoryContext {
        /// <summary>
        /// Gets or sets the callback that resolves one stable scene entity id into the corresponding live editor entity.
        /// </summary>
        public Func<uint, EditorEntity> ResolveEntityById { get; set; }

        /// <summary>
        /// Gets or sets the callback that captures one live editor entity into one detached serialized history snapshot.
        /// </summary>
        public Func<EditorEntity, SerializedEditorEntityState> CaptureEntity { get; set; }

        /// <summary>
        /// Gets or sets the callback that restores one detached serialized entity snapshot back into the live editor scene.
        /// </summary>
        public Func<SerializedEditorEntityState, EditorEntity> RestoreEntity { get; set; }

        /// <summary>
        /// Gets or sets the callback that deletes one live scene entity by stable id.
        /// </summary>
        public Action<uint> DeleteEntityById { get; set; }

        /// <summary>
        /// Gets or sets the callback that reparents one live scene entity to the supplied parent id, or to the scene root when the id is zero.
        /// </summary>
        public Action<uint, uint> ReparentEntity { get; set; }

        /// <summary>
        /// Gets or sets the callback that captures the current live scene settings into one detached history snapshot.
        /// </summary>
        public Func<SceneSettingsAsset> CaptureSceneSettings { get; set; }

        /// <summary>
        /// Gets or sets the callback that applies one detached scene settings snapshot to the live editor scene.
        /// </summary>
        public Action<SceneSettingsAsset> ApplySceneSettings { get; set; }

        /// <summary>
        /// Gets or sets the callback that selects one live scene entity by stable id.
        /// </summary>
        public Action<uint> RestoreSelectionByEntityId { get; set; }

        /// <summary>
        /// Gets or sets the callback that clears the current scene selection.
        /// </summary>
        public Action ClearSelection { get; set; }

        /// <summary>
        /// Gets or sets the callback that refreshes editor UI and derived dirty-state after one history mutation applies.
        /// </summary>
        public Action RefreshEditorState { get; set; }
    }
}
