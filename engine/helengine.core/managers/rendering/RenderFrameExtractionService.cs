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
            List<RenderFrameDrawableSubmission> drawableSubmissions = new List<RenderFrameDrawableSubmission>(drawables.Count);
            List<RenderFrameShadowCasterSubmission> shadowCasterSubmissions = new List<RenderFrameShadowCasterSubmission>(drawables.Count);
            for (int drawableIndex = 0; drawableIndex < drawables.Count; drawableIndex++) {
                AppendDrawableSubmissions(
                    classifier,
                    drawables[drawableIndex],
                    drawableSubmissions,
                    shadowCasterSubmissions);
            }

            RenderFrameLightClassifier lightClassifier = new RenderFrameLightClassifier();
            RenderFrameLightSubmission[] lightSubmissions = new RenderFrameLightSubmission[lights.Count];
            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                lightSubmissions[lightIndex] = lightClassifier.Classify(lights[lightIndex]);
            }

            RenderFrameDrawableSubmission[] drawableSubmissionArray = drawableSubmissions.ToArray();
            RenderFrameShadowCasterSubmission[] shadowCasterSubmissionArray = shadowCasterSubmissions.ToArray();
            NativeOwnership.DetachOwned(drawableSubmissions);
            NativeOwnership.DetachOwned(shadowCasterSubmissions);
            NativeOwnership.Delete(drawableSubmissions);
            NativeOwnership.Delete(shadowCasterSubmissions);
            NativeOwnership.Delete(classifier);
            NativeOwnership.Delete(lightClassifier);

            RenderFrame[] frames = new RenderFrame[cameras.Count];
            for (int index = 0; index < cameras.Count; index++) {
                frames[index] = new RenderFrame(
                    cameras[index],
                    drawableSubmissionArray,
                    lightSubmissions,
                    shadowCasterSubmissionArray);
            }

            NativeOwnership.DeleteItemsAndRelease(ref drawableSubmissionArray);
            NativeOwnership.DeleteItemsAndRelease(ref lightSubmissions);
            NativeOwnership.DeleteItemsAndRelease(ref shadowCasterSubmissionArray);

            return new RenderFrameExtractionResult(frames, backendCapabilities);
        }

        /// <summary>
        /// Classifies one drawable, appends its frame records, and releases the temporary classification array.
        /// </summary>
        /// <param name="classifier">Borrowed classifier used to create submission records.</param>
        /// <param name="drawable">Borrowed drawable to classify.</param>
        /// <param name="drawableSubmissions">Borrowed destination list receiving drawable records.</param>
        /// <param name="shadowCasterSubmissions">Borrowed destination list receiving eligible shadow records.</param>
        static void AppendDrawableSubmissions(
            [NativeNoEscape] RenderFrameDrawableClassifier classifier,
            IDrawable3D drawable,
            [NativeNoEscape] List<RenderFrameDrawableSubmission> drawableSubmissions,
            [NativeNoEscape] List<RenderFrameShadowCasterSubmission> shadowCasterSubmissions) {
            RenderFrameDrawableSubmission[] submissions = classifier.Classify(drawable);
            for (int submissionIndex = 0; submissionIndex < submissions.Length; submissionIndex++) {
                RenderFrameDrawableSubmission submission = submissions[submissionIndex];
                drawableSubmissions.Add(submission);
                if (!submission.IsTransparent && ShouldCastShadows(submission.Material) && SupportsShadowCasting(submission)) {
                    shadowCasterSubmissions.Add(new RenderFrameShadowCasterSubmission(
                        submission.Drawable,
                        submission.SubmeshIndex,
                        submission.Material));
                }
            }

            NativeOwnership.Delete(submissions);
        }

        static bool ShouldCastShadows(RuntimeMaterial material) {
            return material == null || material.CastsShadows;
        }

        static bool SupportsShadowCasting(RenderFrameDrawableSubmission submission) {
            if (submission == null) {
                return true;
            }

            IDrawable3D drawable = submission.Drawable;
            if (drawable == null) {
                return true;
            }

            RuntimeModel model = drawable.Model;
            if (model == null || model.Submeshes == null) {
                return true;
            }

            RuntimeSubmesh[] submeshes = model.Submeshes;
            if (submission.SubmeshIndex < 0 || submission.SubmeshIndex >= submeshes.Length) {
                return true;
            }

            RuntimeSubmesh submesh = submeshes[submission.SubmeshIndex];
            return submesh == null || submesh.PrimitiveTopology == ModelPrimitiveTopology.TriangleList;
        }
    }
}
