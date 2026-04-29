namespace helengine {
    /// <summary>
    /// Stores one persisted component payload inside a scene entity record.
    /// </summary>
    public class SceneComponentAssetRecord {
        /// <summary>
        /// Gets or sets the stable serialized type identifier for the component.
        /// </summary>
        public string ComponentTypeId { get; set; }

        /// <summary>
        /// Gets or sets the entity-local component index used to preserve component ordering.
        /// </summary>
        public int ComponentIndex { get; set; }

        /// <summary>
        /// Gets or sets the opaque component payload bytes.
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}
