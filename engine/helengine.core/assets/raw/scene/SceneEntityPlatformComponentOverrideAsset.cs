namespace helengine {
    /// <summary>
    /// Stores one platform-specific component existence override set attached to a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformComponentOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns the component existence overrides.
        /// </summary>
        public string PlatformId { get; set; }

        /// <summary>
        /// Gets or sets the stable keys for common components removed on the owning platform.
        /// </summary>
        public string[] RemovedComponentKeys { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the platform-only components added on the owning platform.
        /// </summary>
        public SceneEntityPlatformAddedComponentAsset[] AddedComponents { get; set; } = Array.Empty<SceneEntityPlatformAddedComponentAsset>();
    }
}
