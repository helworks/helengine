namespace helengine {
    /// <summary>
    /// Builds validated perspective projections from authored camera state.
    /// </summary>
    public static class CameraProjectionUtils {
        /// <summary>
        /// Minimum legal near clip-plane distance used by validated perspective projections.
        /// </summary>
        public const float MinimumNearPlaneDistance = 0.01f;

        /// <summary>
        /// Minimum legal separation preserved between the near and far clip planes.
        /// </summary>
        public const float MinimumPlaneSeparation = 0.01f;

        /// <summary>
        /// Clamps one near clip-plane distance against the current far clip plane.
        /// </summary>
        /// <param name="nearPlaneDistance">Requested near clip-plane distance.</param>
        /// <param name="farPlaneDistance">Current far clip-plane distance.</param>
        /// <returns>Legal near clip-plane distance for the provided projection state.</returns>
        public static float ClampNearPlaneDistance(float nearPlaneDistance, float farPlaneDistance) {
            float minimumFarPlaneDistance = ClampFarPlaneDistance(MinimumNearPlaneDistance, farPlaneDistance);
            return Math.Min(Math.Max(MinimumNearPlaneDistance, nearPlaneDistance), minimumFarPlaneDistance - MinimumPlaneSeparation);
        }

        /// <summary>
        /// Clamps one far clip-plane distance against the current near clip plane.
        /// </summary>
        /// <param name="nearPlaneDistance">Current near clip-plane distance.</param>
        /// <param name="farPlaneDistance">Requested far clip-plane distance.</param>
        /// <returns>Legal far clip-plane distance for the provided projection state.</returns>
        public static float ClampFarPlaneDistance(float nearPlaneDistance, float farPlaneDistance) {
            float minimumNearPlaneDistance = Math.Max(MinimumNearPlaneDistance, nearPlaneDistance);
            return Math.Max(minimumNearPlaneDistance + MinimumPlaneSeparation, farPlaneDistance);
        }

        /// <summary>
        /// Creates a validated perspective projection matrix for one camera.
        /// </summary>
        /// <param name="camera">Camera providing clip-plane distances.</param>
        /// <param name="fieldOfView">Vertical field of view in radians.</param>
        /// <param name="aspectRatio">Viewport aspect ratio.</param>
        /// <returns>Perspective projection matrix built from validated clip-plane values.</returns>
        public static float4x4 CreatePerspectiveProjection(ICamera camera, float fieldOfView, float aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            float nearPlaneDistance = ClampNearPlaneDistance(camera.NearPlaneDistance, camera.FarPlaneDistance);
            float farPlaneDistance = ClampFarPlaneDistance(nearPlaneDistance, camera.FarPlaneDistance);
            float4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance, out float4x4 projection);
            return projection;
        }
    }
}
