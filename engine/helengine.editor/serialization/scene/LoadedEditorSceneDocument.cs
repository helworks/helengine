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

        /// <summary>
        /// Gets or sets the scene-owned runtime assets materialized while loading the editor scene.
        /// </summary>
        public RuntimeSceneOwnedAssetSet OwnedAssets { get; set; } = new RuntimeSceneOwnedAssetSet(
            Array.Empty<RuntimeTexture>(),
            Array.Empty<FontAsset>(),
            Array.Empty<AudioAsset>(),
            Array.Empty<RuntimeModel>(),
            Array.Empty<RuntimeMaterial>());
    }
}
