namespace helengine {
    /// <summary>
    /// Stores one platform-only component record attached to a serialized scene entity.
    /// </summary>
    public class SceneEntityPlatformAddedComponentAsset {
        /// <summary>
        /// Gets or sets the serialized component record authored only for the owning platform.
        /// </summary>
        public SceneComponentAssetRecord Component { get; set; }
    }
}
