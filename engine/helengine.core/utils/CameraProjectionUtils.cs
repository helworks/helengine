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
        /// Default vertical field of view in radians, 45 degrees. Platform renderers used to
        /// choose this value individually, so the same authored scene framed differently on
        /// each platform; this is the value they all happened to pick, which keeps existing
        /// scenes framed exactly as before.
        /// </summary>
        public const float DefaultFieldOfView = 0.7853982f;

        /// <summary>
        /// Minimum legal vertical field of view in radians, one degree.
        /// </summary>
        public const float MinimumFieldOfView = 0.0174533f;

        /// <summary>
        /// Maximum legal vertical field of view in radians, 175 degrees. The projection
        /// builder rejects a field of view of pi or wider.
        /// </summary>
        public const float MaximumFieldOfView = 3.0543262f;

        /// <summary>
        /// Clamps one vertical field of view into the range the projection builder accepts.
        /// </summary>
        /// <param name="fieldOfView">Requested vertical field of view in radians.</param>
        /// <returns>Legal vertical field of view in radians.</returns>
        public static float ClampFieldOfView(float fieldOfView) {
            return Math.Min(Math.Max(MinimumFieldOfView, fieldOfView), MaximumFieldOfView);
        }

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
        /// <summary>
        /// Creates a validated perspective projection matrix using the camera's own field of view.
        /// </summary>
        /// <param name="camera">Camera providing the field of view and clip-plane distances.</param>
        /// <param name="aspectRatio">Viewport aspect ratio.</param>
        /// <returns>Perspective projection matrix built from validated camera state.</returns>
        public static float4x4 CreatePerspectiveProjection(ICamera camera, float aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return CreatePerspectiveProjection(camera, camera.FieldOfView, aspectRatio);
        }

        public static float4x4 CreatePerspectiveProjection(ICamera camera, float fieldOfView, float aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            float nearPlaneDistance = ClampNearPlaneDistance(camera.NearPlaneDistance, camera.FarPlaneDistance);
            float farPlaneDistance = ClampFarPlaneDistance(nearPlaneDistance, camera.FarPlaneDistance);
            float4x4.CreatePerspectiveFieldOfView(ClampFieldOfView(fieldOfView), aspectRatio, nearPlaneDistance, farPlaneDistance, out float4x4 projection);
            return projection;
        }
    }
}
