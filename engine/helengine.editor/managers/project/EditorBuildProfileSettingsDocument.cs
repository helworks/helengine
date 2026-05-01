namespace helengine.editor {
    /// <summary>
    /// Represents the persisted build-profile defaults used when cooking assets for one platform.
    /// </summary>
    public sealed class EditorBuildProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the percentage used to scale textures during cooking.
        /// </summary>
        public int TextureScalePercent { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether shader variant pruning is enabled for this platform.
        /// </summary>
        public bool ShaderVariantPruningEnabled { get; set; } = true;
    }
}
