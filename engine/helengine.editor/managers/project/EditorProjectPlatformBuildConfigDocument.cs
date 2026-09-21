namespace helengine.editor {
    /// <summary>
    /// Represents one platform's project-shared scene package: which scenes the platform ships and in what order.
    /// </summary>
    public sealed class EditorProjectPlatformBuildConfigDocument {
        /// <summary>
        /// Gets or sets the platform identifier this scene package belongs to.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project-relative scene identifiers in the package, resolved from the persisted references.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> SelectedSceneIds { get; set; } = [];

        /// <summary>
        /// Gets or sets canonical stable references for the scenes in the package.
        /// </summary>
        public List<SceneAssetReference> SelectedSceneReferences { get; set; } = [];

        /// <summary>
        /// Gets or sets the per-scene ordering values used to sort the package before a build is queued.
        /// </summary>
        public List<EditorBuildSceneOrderDocument> SceneOrders { get; set; } = [];
    }
}
