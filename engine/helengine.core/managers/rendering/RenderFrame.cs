namespace helengine {
    /// <summary>
    /// Captures the extracted render data for one camera.
    /// </summary>
    public class RenderFrame {
        /// <summary>
        /// Initializes one extracted render frame.
        /// </summary>
        /// <param name="camera">Camera associated with the frame.</param>
        /// <param name="drawableSubmissions">Visible drawable submissions.</param>
        /// <param name="lightSubmissions">Visible light submissions.</param>
        /// <param name="shadowCasterSubmissions">Visible shadow-caster submissions.</param>
        public RenderFrame(
            CameraComponent camera,
            IReadOnlyList<RenderFrameDrawableSubmission> drawableSubmissions,
            IReadOnlyList<RenderFrameLightSubmission> lightSubmissions,
            IReadOnlyList<RenderFrameShadowCasterSubmission> shadowCasterSubmissions) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            DrawableSubmissions = drawableSubmissions ?? throw new ArgumentNullException(nameof(drawableSubmissions));
            LightSubmissions = lightSubmissions ?? throw new ArgumentNullException(nameof(lightSubmissions));
            ShadowCasterSubmissions = shadowCasterSubmissions ?? throw new ArgumentNullException(nameof(shadowCasterSubmissions));
        }

        /// <summary>
        /// Gets the camera associated with the frame.
        /// </summary>
        public CameraComponent Camera { get; }

        /// <summary>
        /// Gets the visible drawable submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameDrawableSubmission> DrawableSubmissions { get; }

        /// <summary>
        /// Gets the visible light submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameLightSubmission> LightSubmissions { get; }

        /// <summary>
        /// Gets the visible shadow-caster submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameShadowCasterSubmission> ShadowCasterSubmissions { get; }
    }
}
