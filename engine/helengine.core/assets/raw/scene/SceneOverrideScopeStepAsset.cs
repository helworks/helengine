namespace helengine {
    /// <summary>
    /// One step of a serialized override scope path: the level kind and the node id at that level.
    /// </summary>
    public class SceneOverrideScopeStepAsset {
        /// <summary>
        /// Gets or sets the level kind this step belongs to.
        /// </summary>
        public SceneOverrideScopeStepKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the group, platform or environment id at this step.
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }
}
