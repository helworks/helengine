namespace helengine {
    /// <summary>
    /// Captures one visible light submitted into an extracted render frame.
    /// </summary>
    public class RenderFrameLightSubmission {
        /// <summary>
        /// Initializes one light submission.
        /// </summary>
        /// <param name="light">Visible light associated with the submission.</param>
        public RenderFrameLightSubmission(LightComponent light) {
            Light = light ?? throw new ArgumentNullException(nameof(light));
        }

        /// <summary>
        /// Gets the visible light associated with the submission.
        /// </summary>
        public LightComponent Light { get; }
    }
}
