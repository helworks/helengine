namespace helengine.directx11 {
    /// <summary>
    /// Categorizes extracted drawables into DirectX11 batching buckets.
    /// </summary>
    public sealed class DirectX11BatchingPlanner {
        /// <summary>
        /// Builds the DirectX11 batching plan for the supplied drawable submissions.
        /// </summary>
        /// <param name="drawableSubmissions">Drawable submissions gathered for the frame.</param>
        /// <returns>DirectX11 batching buckets derived from the shared batching metadata.</returns>
        public DirectX11BatchingPlan Build(IReadOnlyList<RenderFrameDrawableSubmission> drawableSubmissions) {
            if (drawableSubmissions == null) {
                throw new ArgumentNullException(nameof(drawableSubmissions));
            }

            List<RenderFrameDrawableSubmission> staticBatchDrawables = new List<RenderFrameDrawableSubmission>();
            List<RenderFrameDrawableSubmission> dynamicBatchDrawables = new List<RenderFrameDrawableSubmission>();
            List<RenderFrameDrawableSubmission> instancedDrawables = new List<RenderFrameDrawableSubmission>();
            for (int index = 0; index < drawableSubmissions.Count; index++) {
                RenderFrameDrawableSubmission drawableSubmission = drawableSubmissions[index];
                RenderFrameBatchingMetadata batchingMetadata = drawableSubmission.BatchingMetadata;
                if (batchingMetadata.IsStaticEligible) {
                    staticBatchDrawables.Add(drawableSubmission);
                }
                if (batchingMetadata.IsDynamicEligible) {
                    dynamicBatchDrawables.Add(drawableSubmission);
                }
                if (batchingMetadata.IsInstancingEligible) {
                    instancedDrawables.Add(drawableSubmission);
                }
            }

            return new DirectX11BatchingPlan(staticBatchDrawables, dynamicBatchDrawables, instancedDrawables);
        }
    }
}
