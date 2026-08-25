namespace helengine.editor {
    /// <summary>
    /// Serialized per-project editor session state stored in `user_settings/editor_session.json`.
    /// </summary>
    public sealed class EditorSessionStateDocument {
        /// <summary>
        /// Gets or sets the canonical stable reference to the last authored scene.
        /// </summary>
        public SceneAssetReference LastSceneReference { get; set; }
    }
}
