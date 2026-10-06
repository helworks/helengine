namespace helengine {
    /// <summary>Orders transparent submissions from far to near for the camera that will composite them.</summary>
    public static class TransparentDepthSorter {
        /// <summary>Sorts frame-owned records while preserving opaque order and existing 3D priority bands.</summary>
        /// <param name="submissions">Frame-owned submissions to arrange in place.</param>
        /// <param name="camera">Camera supplying the view direction.</param>
        public static void Sort(RenderFrameDrawableSubmission[] submissions, CameraComponent camera) {
            if (camera.Parent == null) {
                return;
            }
            float3 forward = float4.RotateVector(new float3(0, 0, -1), camera.Parent.Orientation);
            for (int index = 1; index < submissions.Length; index++) {
                RenderFrameDrawableSubmission candidate = submissions[index];
                int insertionIndex = index;
                while (insertionIndex > 0 && Compare(candidate, submissions[insertionIndex - 1], forward, camera.Parent.Position) < 0) {
                    submissions[insertionIndex] = submissions[insertionIndex - 1];
                    insertionIndex--;
                }
                submissions[insertionIndex] = candidate;
            }
        }

        /// <summary>Compares physical camera depth within each existing 3D render band.</summary>
        /// <param name="left">First submission.</param>
        /// <param name="right">Second submission.</param>
        /// <param name="forward">Camera's world-space forward direction.</param>
        /// <param name="cameraPosition">Camera world position used for planar projection.</param>
        /// <returns>Negative when the first submission should be composited earlier.</returns>
        static int Compare(RenderFrameDrawableSubmission left, RenderFrameDrawableSubmission right, float3 forward, float3 cameraPosition) {
            if (left.IsTransparent != right.IsTransparent) {
                return left.IsTransparent ? 1 : -1;
            }
            if (!left.IsTransparent) {
                return 0;
            }
            return CameraDepthOrder.CompareTransparent(left.Drawable, right.Drawable, forward, cameraPosition);
        }
    }
}
