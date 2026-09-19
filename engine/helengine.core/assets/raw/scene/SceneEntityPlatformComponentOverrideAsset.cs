namespace helengine {
    /// <summary>
    /// Stores one component existence override set authored on one override scope path inside a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformComponentOverrideAsset {
        /// <summary>
        /// Gets or sets the scope path this override is authored on. Empty is Common.
        /// </summary>
        public SceneOverrideScopeStepAsset[] Scope { get; set; } = Array.Empty<SceneOverrideScopeStepAsset>();

        /// <summary>
        /// Gets or sets the stable keys for common components removed on the owning scope.
        /// </summary>
        public string[] RemovedComponentKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the scope-only components added on the owning scope.
        /// </summary>
        public SceneEntityPlatformAddedComponentAsset[] AddedComponents { get; set; } = Array.Empty<SceneEntityPlatformAddedComponentAsset>();
    }
}
