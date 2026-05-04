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
            if (light == null) {
                throw new ArgumentNullException(nameof(light));
            }

            Light = light;
        }

        /// <summary>
        /// Initializes one light submission with an explicit planning importance score.
        /// </summary>
        /// <param name="light">Visible light associated with the submission.</param>
        /// <param name="importance">Relative importance score used during backend light-budget selection.</param>
        public RenderFrameLightSubmission(LightComponent light, int importance) {
            Light = light ?? throw new ArgumentNullException(nameof(light));
            Importance = importance;
        }

        /// <summary>
        /// Gets the visible light associated with the submission.
        /// </summary>
        public LightComponent Light { get; }

        /// <summary>
        /// Gets the authored light family represented by the submission.
        /// </summary>
        public LightType LightType => Light.LightType;

        /// <summary>
        /// Gets the relative importance score used during backend light-budget selection.
        /// </summary>
        public int Importance { get; }
    }
}
