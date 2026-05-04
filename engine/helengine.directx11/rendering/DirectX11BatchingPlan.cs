namespace helengine.directx11 {
    /// <summary>
    /// Stores the categorized batching buckets selected for one DirectX11 render frame.
    /// </summary>
    public sealed class DirectX11BatchingPlan {
        /// <summary>
        /// Initializes one DirectX11 batching plan.
        /// </summary>
        /// <param name="staticBatchDrawables">Drawable submissions that should be considered for static batching.</param>
        /// <param name="dynamicBatchDrawables">Drawable submissions that should be considered for dynamic batching.</param>
        /// <param name="instancedDrawables">Drawable submissions that should be considered for instancing.</param>
        public DirectX11BatchingPlan(
            IReadOnlyList<RenderFrameDrawableSubmission> staticBatchDrawables,
            IReadOnlyList<RenderFrameDrawableSubmission> dynamicBatchDrawables,
            IReadOnlyList<RenderFrameDrawableSubmission> instancedDrawables) {
            StaticBatchDrawables = staticBatchDrawables ?? throw new ArgumentNullException(nameof(staticBatchDrawables));
            DynamicBatchDrawables = dynamicBatchDrawables ?? throw new ArgumentNullException(nameof(dynamicBatchDrawables));
            InstancedDrawables = instancedDrawables ?? throw new ArgumentNullException(nameof(instancedDrawables));
        }

        /// <summary>
        /// Gets the drawable submissions that should be considered for static batching.
        /// </summary>
        public IReadOnlyList<RenderFrameDrawableSubmission> StaticBatchDrawables { get; }

        /// <summary>
        /// Gets the drawable submissions that should be considered for dynamic batching.
        /// </summary>
        public IReadOnlyList<RenderFrameDrawableSubmission> DynamicBatchDrawables { get; }

        /// <summary>
        /// Gets the drawable submissions that should be considered for instancing.
        /// </summary>
        public IReadOnlyList<RenderFrameDrawableSubmission> InstancedDrawables { get; }
    }
}
