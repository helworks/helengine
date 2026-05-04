namespace helengine {
    /// <summary>
    /// Extracts backend-neutral render frame data from visible scene objects.
    /// </summary>
    public class RenderFrameExtractionService {
        /// <summary>
        /// Extracts one frame per camera using the currently visible scene inputs.
        /// </summary>
        /// <param name="cameras">Visible cameras to extract.</param>
        /// <param name="drawables">Visible drawables.</param>
        /// <param name="lights">Visible lights.</param>
        /// <param name="backendCapabilities">Backend capability profile used for extraction.</param>
        /// <returns>Extracted render-frame result.</returns>
        public RenderFrameExtractionResult Extract(
            IReadOnlyList<CameraComponent> cameras,
            IReadOnlyList<IDrawable3D> drawables,
            IReadOnlyList<LightComponent> lights,
            RendererBackendCapabilityProfile backendCapabilities) {
            if (cameras == null) {
                throw new ArgumentNullException(nameof(cameras));
            } else if (drawables == null) {
                throw new ArgumentNullException(nameof(drawables));
            } else if (lights == null) {
                throw new ArgumentNullException(nameof(lights));
            } else if (backendCapabilities == null) {
                throw new ArgumentNullException(nameof(backendCapabilities));
            }

            RenderFrame[] frames = new RenderFrame[cameras.Count];
            for (int index = 0; index < cameras.Count; index++) {
                frames[index] = new RenderFrame(
                    cameras[index],
                    Array.Empty<RenderFrameDrawableSubmission>(),
                    Array.Empty<RenderFrameLightSubmission>(),
                    Array.Empty<RenderFrameShadowCasterSubmission>());
            }

            return new RenderFrameExtractionResult(frames, backendCapabilities);
        }
    }
}
