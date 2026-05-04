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
            Drawable = drawable ?? throw new ArgumentNullException(nameof(drawable));
        }

        /// <summary>
        /// Gets the visible drawable associated with the submission.
        /// </summary>
        public IDrawable3D Drawable { get; }
    }
}
