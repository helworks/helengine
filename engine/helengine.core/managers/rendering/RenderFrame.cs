namespace helengine {
    /// <summary>
    /// Captures the extracted render data for one camera.
    /// </summary>
    public class RenderFrame : IDisposable {
        /// <summary>
        /// Stores frame-owned drawable submission records while their scene objects remain borrowed.
        /// </summary>
        [NativeOwnedMember]
        RenderFrameDrawableSubmission[] DrawableSubmissionsValue;

        /// <summary>
        /// Stores frame-owned light submission records while their light components remain borrowed.
        /// </summary>
        [NativeOwnedMember]
        RenderFrameLightSubmission[] LightSubmissionsValue;

        /// <summary>
        /// Stores frame-owned shadow submission records while their scene objects remain borrowed.
        /// </summary>
        [NativeOwnedMember]
        RenderFrameShadowCasterSubmission[] ShadowCasterSubmissionsValue;

        /// <summary>
        /// Initializes one extracted render frame.
        /// </summary>
        /// <param name="camera">Camera associated with the frame.</param>
        /// <param name="drawableSubmissions">Visible drawable submissions.</param>
        /// <param name="lightSubmissions">Visible light submissions.</param>
        /// <param name="shadowCasterSubmissions">Visible shadow-caster submissions.</param>
        public RenderFrame(
            CameraComponent camera,
            [NativeNoEscape] IReadOnlyList<RenderFrameDrawableSubmission> drawableSubmissions,
            [NativeNoEscape] IReadOnlyList<RenderFrameLightSubmission> lightSubmissions,
            [NativeNoEscape] IReadOnlyList<RenderFrameShadowCasterSubmission> shadowCasterSubmissions) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            DrawableSubmissionsValue = CopyDrawableSubmissions(drawableSubmissions);
            LightSubmissionsValue = CopyLightSubmissions(lightSubmissions);
            ShadowCasterSubmissionsValue = CopyShadowCasterSubmissions(shadowCasterSubmissions);
        }

        /// <summary>
        /// Gets the camera associated with the frame.
        /// </summary>
        public CameraComponent Camera { get; }

        /// <summary>
        /// Gets the visible drawable submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameDrawableSubmission> DrawableSubmissions => DrawableSubmissionsValue;

        /// <summary>
        /// Gets the visible light submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameLightSubmission> LightSubmissions => LightSubmissionsValue;

        /// <summary>
        /// Gets the visible shadow-caster submissions.
        /// </summary>
        public IReadOnlyList<RenderFrameShadowCasterSubmission> ShadowCasterSubmissions => ShadowCasterSubmissionsValue;

        /// <summary>
        /// Gets whether the frame contains any transparent drawables that require a transparent forward pass.
        /// </summary>
        public bool HasTransparentDrawables {
            get {
                for (int index = 0; index < DrawableSubmissions.Count; index++) {
                    if (DrawableSubmissions[index].IsTransparent) {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Releases the submission records and array containers owned by this extracted frame.
        /// </summary>
        public void Dispose() {
            NativeOwnership.DeleteItemsAndRelease(ref DrawableSubmissionsValue);
            NativeOwnership.DeleteItemsAndRelease(ref LightSubmissionsValue);
            NativeOwnership.DeleteItemsAndRelease(ref ShadowCasterSubmissionsValue);
        }

        /// <summary>
        /// Copies drawable submission records so one frame owns its records without owning referenced scene objects.
        /// </summary>
        /// <param name="submissions">Borrowed drawable submissions to copy.</param>
        /// <returns>New frame-owned drawable submission records.</returns>
        static RenderFrameDrawableSubmission[] CopyDrawableSubmissions(
            [NativeNoEscape] IReadOnlyList<RenderFrameDrawableSubmission> submissions) {
            if (submissions == null) {
                throw new ArgumentNullException(nameof(submissions));
            }

            RenderFrameDrawableSubmission[] copies = new RenderFrameDrawableSubmission[submissions.Count];
            for (int index = 0; index < submissions.Count; index++) {
                RenderFrameDrawableSubmission submission = submissions[index];
                copies[index] = new RenderFrameDrawableSubmission(
                    submission.Drawable,
                    submission.SubmeshIndex,
                    submission.Material,
                    submission.IsTransparent,
                    submission.BatchingMetadata);
            }

            return copies;
        }

        /// <summary>
        /// Copies light submission records so one frame owns its records without owning referenced light components.
        /// </summary>
        /// <param name="submissions">Borrowed light submissions to copy.</param>
        /// <returns>New frame-owned light submission records.</returns>
        static RenderFrameLightSubmission[] CopyLightSubmissions(
            [NativeNoEscape] IReadOnlyList<RenderFrameLightSubmission> submissions) {
            if (submissions == null) {
                throw new ArgumentNullException(nameof(submissions));
            }

            RenderFrameLightSubmission[] copies = new RenderFrameLightSubmission[submissions.Count];
            for (int index = 0; index < submissions.Count; index++) {
                RenderFrameLightSubmission submission = submissions[index];
                copies[index] = new RenderFrameLightSubmission(submission.Light, submission.Importance);
            }

            return copies;
        }

        /// <summary>
        /// Copies shadow submission records so one frame owns its records without owning referenced scene objects.
        /// </summary>
        /// <param name="submissions">Borrowed shadow submissions to copy.</param>
        /// <returns>New frame-owned shadow submission records.</returns>
        static RenderFrameShadowCasterSubmission[] CopyShadowCasterSubmissions(
            [NativeNoEscape] IReadOnlyList<RenderFrameShadowCasterSubmission> submissions) {
            if (submissions == null) {
                throw new ArgumentNullException(nameof(submissions));
            }

            RenderFrameShadowCasterSubmission[] copies = new RenderFrameShadowCasterSubmission[submissions.Count];
            for (int index = 0; index < submissions.Count; index++) {
                RenderFrameShadowCasterSubmission submission = submissions[index];
                copies[index] = new RenderFrameShadowCasterSubmission(
                    submission.Drawable,
                    submission.SubmeshIndex,
                    submission.Material);
            }

            return copies;
        }
    }
}
