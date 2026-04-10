namespace helengine.editor {
    /// <summary>
    /// Defines stable processor identifiers used by editor content-loading paths.
    /// </summary>
    public static class EditorContentProcessorIds {
        /// <summary>
        /// Processor id used for serialized material assets.
        /// </summary>
        public const string MaterialAsset = "editor.material-asset";
        /// <summary>
        /// Processor id used for serialized model assets.
        /// </summary>
        public const string ModelAsset = "editor.model-asset";
        /// <summary>
        /// Processor id used for serialized texture assets.
        /// </summary>
        public const string TextureAsset = "editor.texture-asset";
        /// <summary>
        /// Processor id used for serialized text assets.
        /// </summary>
        public const string TextAsset = "editor.text-asset";
        /// <summary>
        /// Processor id used for serialized shader assets.
        /// </summary>
        public const string ShaderAsset = "editor.shader-asset";
        /// <summary>
        /// Processor id used for serialized scene assets.
        /// </summary>
        public const string SceneAsset = "editor.scene-asset";
        /// <summary>
        /// Processor id used for serialized asset import settings sidecars.
        /// </summary>
        public const string AssetImportSettings = "editor.asset-import-settings";
    }
}
