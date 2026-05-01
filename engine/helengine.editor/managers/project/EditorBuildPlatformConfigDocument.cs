namespace helengine.editor {
    /// <summary>
    /// Represents one platform tab's persisted local build configuration.
    /// </summary>
    public sealed class EditorBuildPlatformConfigDocument {
        /// <summary>
        /// Gets or sets the platform identifier this local build configuration belongs to.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project-relative scene identifiers selected for this platform.
        /// </summary>
        public List<string> SelectedSceneIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the per-scene ordering values used to sort selected scenes before a build request is queued.
        /// </summary>
        public List<EditorBuildSceneOrderDocument> SceneOrders { get; set; } = [];

        /// <summary>
        /// Gets or sets the last output directory path chosen for this platform.
        /// </summary>
        public string OutputDirectoryPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this platform should default to a debug native player build.
        /// </summary>
        public bool DebugBuild { get; set; }
    }
}
