namespace helengine {
    /// <summary>
    /// Captures one visible drawable submitted into an extracted render frame.
    /// </summary>
    public class RenderFrameDrawableSubmission {
        /// <summary>
        /// Initializes one drawable submission.
        /// </summary>
        /// <param name="drawable">Visible drawable associated with the submission.</param>
        public RenderFrameDrawableSubmission(IDrawable3D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            Drawable = drawable;
            BatchingMetadata = new RenderFrameBatchingMetadata(false, false, false);
            RuntimeMaterial[] materials = drawable.Materials;
            Material = materials != null && materials.Length > 0 ? materials[0] : null;
        }

        /// <summary>
        /// Initializes one drawable submission with explicit transparency and batching metadata.
        /// </summary>
        /// <param name="drawable">Visible drawable associated with the submission.</param>
        /// <param name="isTransparent">Whether the drawable should be treated as transparent during pass planning.</param>
        /// <param name="batchingMetadata">Shared batching metadata associated with the drawable.</param>
        public RenderFrameDrawableSubmission(IDrawable3D drawable, bool isTransparent, RenderFrameBatchingMetadata batchingMetadata) {
            Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
            BatchingMetadata = batchingMetadata ?? throw new ArgumentNullException(nameof(batchingMetadata));
            RuntimeMaterial[] materials = drawable.Materials;
            Material = materials != null && materials.Length > 0 ? materials[0] : null;
            IsTransparent = isTransparent;
        }

        /// <summary>
        /// Initializes one drawable submission with explicit submesh, material, transparency, and batching metadata.
        /// </summary>
        /// <param name="drawable">Visible drawable associated with the submission.</param>
        /// <param name="submeshIndex">Zero-based submesh index represented by the submission.</param>
        /// <param name="material">Runtime material resolved for the submission.</param>
        /// <param name="isTransparent">Whether the drawable should be treated as transparent during pass planning.</param>
        /// <param name="batchingMetadata">Shared batching metadata associated with the drawable.</param>
        public RenderFrameDrawableSubmission(
            IDrawable3D drawable,
            int submeshIndex,
            RuntimeMaterial material,
            bool isTransparent,
            RenderFrameBatchingMetadata batchingMetadata) {
            Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
            if (submeshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), "Submesh index must be non-negative.");
            }

            BatchingMetadata = batchingMetadata ?? throw new ArgumentNullException(nameof(batchingMetadata));
            SubmeshIndex = submeshIndex;
            Material = material;
            IsTransparent = isTransparent;
        }

        /// <summary>
        /// Gets the visible drawable associated with the submission.
        /// </summary>
        public IDrawable3D Drawable { get; }

        /// <summary>
        /// Gets the zero-based submesh index represented by the submission.
        /// </summary>
        public int SubmeshIndex { get; }

        /// <summary>
        /// Gets the runtime material resolved for the submission.
        /// </summary>
        public RuntimeMaterial Material { get; }

        /// <summary>
        /// Gets whether the drawable should be planned in the transparent forward pass.
        /// </summary>
        public bool IsTransparent { get; }

        /// <summary>
        /// Gets the shared batching metadata associated with the drawable.
        /// </summary>
        public RenderFrameBatchingMetadata BatchingMetadata { get; }
    }
}
