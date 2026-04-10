namespace helengine {
    /// <summary>
    /// Stores one serialized entity inside a scene asset.
    /// </summary>
    public class SceneEntityAsset {
        /// <summary>
        /// Gets or sets the display name shown for the serialized entity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the local position relative to the serialized parent.
        /// </summary>
        public float3 LocalPosition { get; set; }

        /// <summary>
        /// Gets or sets the local scale relative to the serialized parent.
        /// </summary>
        public float3 LocalScale { get; set; }

        /// <summary>
        /// Gets or sets the local orientation relative to the serialized parent.
        /// </summary>
        public float4 LocalOrientation { get; set; }

        /// <summary>
        /// Gets or sets the serialized component payloads attached to the entity.
        /// </summary>
        public SceneComponentAssetRecord[] Components { get; set; } = Array.Empty<SceneComponentAssetRecord>();

        /// <summary>
        /// Gets or sets the serialized child entities owned by the entity.
        /// </summary>
        public SceneEntityAsset[] Children { get; set; } = Array.Empty<SceneEntityAsset>();
    }
}
