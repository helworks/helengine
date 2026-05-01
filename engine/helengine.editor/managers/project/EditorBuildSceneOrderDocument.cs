namespace helengine.editor {
    /// <summary>
    /// Represents one persisted ordering entry for a scene in the local build dialog.
    /// </summary>
    public sealed class EditorBuildSceneOrderDocument {
        /// <summary>
        /// Gets or sets the project-relative scene identifier whose order is being tracked.
        /// </summary>
        public string SceneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the 1-based ordering number assigned to the scene.
        /// </summary>
        public int OrderNumber { get; set; } = 1;
    }
}
