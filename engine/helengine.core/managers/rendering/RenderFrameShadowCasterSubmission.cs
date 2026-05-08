namespace helengine {
    /// <summary>
    /// Captures one drawable that may contribute to a shadow pass.
    /// </summary>
    public class RenderFrameShadowCasterSubmission {
        /// <summary>
        /// Initializes one shadow-caster submission.
        /// </summary>
        /// <param name="drawable">Visible drawable associated with the shadow submission.</param>
        public RenderFrameShadowCasterSubmission(IDrawable3D drawable) {
            Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
            Material = drawable.Material;
        }

        /// <summary>
        /// Initializes one shadow-caster submission with explicit submesh and material metadata.
        /// </summary>
        /// <param name="drawable">Visible drawable associated with the shadow submission.</param>
        /// <param name="submeshIndex">Zero-based submesh index represented by the submission.</param>
        /// <param name="material">Runtime material resolved for the submission.</param>
        public RenderFrameShadowCasterSubmission(IDrawable3D drawable, int submeshIndex, RuntimeMaterial material) {
            Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
            if (submeshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(submeshIndex), "Submesh index must be non-negative.");
            }

            SubmeshIndex = submeshIndex;
            Material = material;
        }

        /// <summary>
        /// Gets the drawable associated with the shadow-caster submission.
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
    }
}
