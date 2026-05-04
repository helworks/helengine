namespace helengine.editor {
    /// <summary>
    /// Represents the persisted graphics-profile defaults used by one platform build.
    /// </summary>
    public sealed class EditorGraphicsProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the selected builder-provided graphics profile id.
        /// </summary>
        public string SelectedGraphicsProfileId { get; set; } = string.Empty;

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

        /// <summary>
        /// Gets or sets the renderer-wide depth-prepass mode requested by this platform profile.
        /// </summary>
        public DepthPrepassMode RendererDepthPrepassMode { get; set; } = DepthPrepassMode.Auto;

        /// <summary>
        /// Gets or sets the renderer-wide shadow quality tier identifier requested by this platform profile.
        /// </summary>
        public string RendererShadowQualityTier { get; set; } = "medium";

        /// <summary>
        /// Gets or sets whether HDR rendering should be enabled by default for this platform profile.
        /// </summary>
        public bool RendererHdrEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the renderer-wide post-processing tier requested by this platform profile.
        /// </summary>
        public PostProcessTier RendererPostProcessTier { get; set; } = PostProcessTier.High;

        /// <summary>
        /// Gets or sets the builder-provided graphics option values keyed by setting id.
        /// </summary>
        public Dictionary<string, string> SelectedOptionValues { get; set; } = [];
    }
}
