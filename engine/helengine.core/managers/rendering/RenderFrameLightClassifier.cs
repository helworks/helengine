namespace helengine {
    /// <summary>
    /// Classifies authored light components into extracted render-frame light submissions.
    /// </summary>
    public sealed class RenderFrameLightClassifier {
        /// <summary>
        /// Classifies one authored light into the shared render-frame representation.
        /// </summary>
        /// <param name="light">Authored light to classify.</param>
        /// <returns>Extracted render-frame light submission with backend selection importance.</returns>
        public RenderFrameLightSubmission Classify(LightComponent light) {
            if (light == null) {
                throw new ArgumentNullException(nameof(light));
            }

            int importance = 0;
            if (light.ShadowsEnabled) {
                importance += 1000;
            }

            importance += (int)Math.Round(light.Intensity * 100.0, MidpointRounding.AwayFromZero);
            return new RenderFrameLightSubmission(light, importance);
        }
    }
}
