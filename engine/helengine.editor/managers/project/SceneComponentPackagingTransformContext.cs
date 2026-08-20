namespace helengine.editor {
    /// <summary>
    /// Supplies entity-specific cook-time data needed while one serialized component record is transformed for a target platform.
    /// </summary>
    public sealed class SceneComponentPackagingTransformContext {
        /// <summary>
        /// Gets the final static world scale of the entity that owns the component record.
        /// </summary>
        public float3 WorldScale { get; }

        /// <summary>
        /// Gets the final static world position of the entity that owns the component record.
        /// </summary>
        public float3 WorldPosition { get; }

        /// <summary>
        /// Gets the final static world orientation of the entity that owns the component record.
        /// </summary>
        public float4 WorldOrientation { get; }

        /// <summary>
        /// Initializes a context for one component record using its final static world scale and an identity world pose.
        /// </summary>
        /// <param name="worldScale">Final static world scale of the owning entity.</param>
        public SceneComponentPackagingTransformContext(float3 worldScale)
            : this(worldScale, float3.Zero, float4.Identity) {
        }

        /// <summary>
        /// Initializes a context for one component record using its complete final static world transform.
        /// </summary>
        /// <param name="worldScale">Final static world scale of the owning entity.</param>
        /// <param name="worldPosition">Final static world position of the owning entity.</param>
        /// <param name="worldOrientation">Final static world orientation of the owning entity.</param>
        public SceneComponentPackagingTransformContext(float3 worldScale, float3 worldPosition, float4 worldOrientation) {
            WorldScale = worldScale;
            WorldPosition = worldPosition;
            WorldOrientation = worldOrientation;
        }
    }
}
