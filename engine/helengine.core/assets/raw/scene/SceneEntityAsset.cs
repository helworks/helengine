namespace helengine {
    /// <summary>
    /// Stores one serialized entity inside a scene asset.
    /// </summary>
    public class SceneEntityAsset {
        /// <summary>
        /// Tracks the number of transient entity records that have been constructed and not explicitly released by the runtime scene loader.
        /// </summary>
        static int LiveInstanceCountValue;

        /// <summary>
        /// Initializes a serialized entity record and records the transient diagnostic lifetime.
        /// </summary>
        public SceneEntityAsset() {
            LiveInstanceCountValue++;
        }

        /// <summary>
        /// Gets the number of serialized entity records currently considered live by transient release diagnostics.
        /// </summary>
        public static int LiveInstanceCount => LiveInstanceCountValue;

        /// <summary>
        /// Marks this serialized entity record as released by the runtime transient-scene cleanup path.
        /// </summary>
        public void MarkReleasedForDiagnostics() {
            LiveInstanceCountValue--;
        }

        /// <summary>
        /// Gets or sets the stable id assigned to the serialized entity.
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// Gets or sets the display name shown for the serialized entity.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the serialized entity should be restored as static.
        /// </summary>
        public bool IsStatic { get; set; }

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
        /// Gets or sets the editor-authored per-platform transform overrides attached to the entity.
        /// </summary>
        public SceneEntityPlatformTransformOverrideAsset[] PlatformTransformOverrides { get; set; } = Array.Empty<SceneEntityPlatformTransformOverrideAsset>();

        /// <summary>
        /// Gets or sets the editor-authored per-platform component existence overrides attached to the entity.
        /// </summary>
        public SceneEntityPlatformComponentOverrideAsset[] PlatformComponentOverrides { get; set; } = Array.Empty<SceneEntityPlatformComponentOverrideAsset>();

        /// <summary>
        /// Gets or sets the serialized child entities owned by the entity.
        /// </summary>
        public SceneEntityAsset[] Children { get; set; } = Array.Empty<SceneEntityAsset>();
    }
}
