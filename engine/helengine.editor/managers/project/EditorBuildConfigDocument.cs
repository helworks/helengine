namespace helengine.editor {
    /// <summary>
    /// Represents the local build configuration persisted in `user_settings/build_config.json`.
    /// </summary>
    public sealed class EditorBuildConfigDocument {
        /// <summary>Gets or sets the default cooperative scene-loading budget per frame in milliseconds; indivisible IO and callbacks can exceed it.</summary>
        public double SceneLoadingBudgetMilliseconds { get; set; } = 8d;

        /// <summary>Gets or sets the default packaged loading-screen scene id; empty means no configured loading-screen scene.</summary>
        public string LoadingScreenSceneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the per-platform local build configuration collection.
        /// </summary>
        public List<EditorBuildPlatformConfigDocument> Platforms { get; set; } = [];

        /// <summary>
        /// Gets or sets the persisted build queue entries for the current project.
        /// </summary>
        public List<EditorBuildQueueItemDocument> QueueItems { get; set; } = [];
    }
}
