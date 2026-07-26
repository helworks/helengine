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
        /// Initializes a context for one component record using its final static world scale.
        /// </summary>
        /// <param name="worldScale">Final static world scale of the owning entity.</param>
        public SceneComponentPackagingTransformContext(float3 worldScale) {
            WorldScale = worldScale;
        }
    }
}
