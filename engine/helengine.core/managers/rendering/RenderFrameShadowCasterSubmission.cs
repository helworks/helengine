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
        }

        /// <summary>
        /// Gets the drawable associated with the shadow-caster submission.
        /// </summary>
        public IDrawable3D Drawable { get; }
    }
}
