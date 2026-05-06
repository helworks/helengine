namespace helengine.editor {
    /// <summary>
    /// Represents one loaded editor scene document, including live root entities and scene-level settings.
    /// </summary>
    public class LoadedEditorSceneDocument {
        /// <summary>
        /// Gets or sets the live root entities materialized from the scene file.
        /// </summary>
        public EditorEntity[] RootEntities { get; set; } = Array.Empty<EditorEntity>();

        /// <summary>
        /// Gets or sets the scene-level settings loaded from the scene file.
        /// </summary>
        public SceneSettingsAsset SceneSettings { get; set; } = new SceneSettingsAsset();
    }
}
