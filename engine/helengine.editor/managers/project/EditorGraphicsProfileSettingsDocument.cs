namespace helengine.editor {
    /// <summary>
    /// Represents the persisted graphics-profile defaults used by one platform build.
    /// </summary>
    public sealed class EditorGraphicsProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the default runtime backbuffer width.
        /// </summary>
        public int DefaultWidth { get; set; } = 1280;

        /// <summary>
        /// Gets or sets the default runtime backbuffer height.
        /// </summary>
        public int DefaultHeight { get; set; } = 720;

        /// <summary>
        /// Gets or sets whether the runtime player should enable vsync by default.
        /// </summary>
        public bool VSyncEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the runtime player should start in fullscreen mode.
        /// </summary>
        public bool FullscreenEnabled { get; set; } = false;
    }
}
