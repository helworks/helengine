namespace helengine {
    /// <summary>
    /// Classifies visible drawables into the shared render-frame representation.
    /// </summary>
    public sealed class RenderFrameDrawableClassifier {
        /// <summary>
        /// Creates one shared drawable submission from a visible runtime drawable.
        /// </summary>
        /// <param name="drawable">Visible drawable to classify.</param>
        /// <returns>Shared render-frame drawable submission.</returns>
        public RenderFrameDrawableSubmission Classify(IDrawable3D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            RuntimeMaterial material = drawable.Material;
            bool isTransparent = false;
            if (material != null) {
                MaterialRenderState renderState = material.RenderState;
                if (renderState != null && renderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                    isTransparent = true;
                }
            }

            return new RenderFrameDrawableSubmission(
                drawable,
                isTransparent,
                new RenderFrameBatchingMetadata(false, false, false));
        }
    }
}
