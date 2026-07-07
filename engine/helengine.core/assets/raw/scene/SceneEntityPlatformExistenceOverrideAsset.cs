namespace helengine {
    /// <summary>
    /// Stores one platform-specific entity existence override attached to a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformExistenceOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns this entity existence override.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the entity should exist on the owning platform.
        /// </summary>
        public bool Exists { get; set; }
    }
}
