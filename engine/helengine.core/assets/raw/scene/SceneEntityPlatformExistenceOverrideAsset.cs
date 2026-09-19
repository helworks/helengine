namespace helengine {
    /// <summary>
    /// Stores one entity existence override authored on one override scope path inside a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformExistenceOverrideAsset {
        /// <summary>
        /// Gets or sets the scope path this override is authored on. Empty is Common.
        /// </summary>
        public SceneOverrideScopeStepAsset[] Scope { get; set; } = Array.Empty<SceneOverrideScopeStepAsset>();

        /// <summary>
        /// Gets or sets a value indicating whether the entity should exist on the owning scope.
        /// </summary>
        public bool Exists { get; set; }
    }
}
