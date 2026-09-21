namespace helengine.editor {
    /// <summary>
    /// Represents the project-shared build configuration persisted in `settings/build_config.json`: the scene package
    /// each platform ships. Everything a developer changes for their own machine lives in the local document instead.
    /// </summary>
    public sealed class EditorProjectBuildConfigDocument {
        /// <summary>
        /// Gets or sets the per-platform scene packages.
        /// </summary>
        public List<EditorProjectPlatformBuildConfigDocument> Platforms { get; set; } = [];
    }
}
