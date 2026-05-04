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

            RenderFrameDrawableClassifier classifier = new RenderFrameDrawableClassifier();
            RenderFrameDrawableSubmission[] drawableSubmissions = new RenderFrameDrawableSubmission[drawables.Count];
            List<RenderFrameShadowCasterSubmission> shadowCasterSubmissions = new List<RenderFrameShadowCasterSubmission>(drawables.Count);
            for (int drawableIndex = 0; drawableIndex < drawables.Count; drawableIndex++) {
                IDrawable3D drawable = drawables[drawableIndex];
                RenderFrameDrawableSubmission submission = classifier.Classify(drawable);
                drawableSubmissions[drawableIndex] = submission;
                if (!submission.IsTransparent) {
                    shadowCasterSubmissions.Add(new RenderFrameShadowCasterSubmission(drawable));
                }
            }

            RenderFrameLightClassifier lightClassifier = new RenderFrameLightClassifier();
            RenderFrameLightSubmission[] lightSubmissions = new RenderFrameLightSubmission[lights.Count];
            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                lightSubmissions[lightIndex] = lightClassifier.Classify(lights[lightIndex]);
            }

            RenderFrame[] frames = new RenderFrame[cameras.Count];
            for (int index = 0; index < cameras.Count; index++) {
                frames[index] = new RenderFrame(
                    cameras[index],
                    drawableSubmissions,
                    lightSubmissions,
                    shadowCasterSubmissions.ToArray());
            }

            return new RenderFrameExtractionResult(frames, backendCapabilities);
        }
    }
}
