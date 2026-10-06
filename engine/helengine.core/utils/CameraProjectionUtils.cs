namespace helengine {
    /// <summary>
    /// Builds validated perspective and orthographic projections from camera state.
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
        /// Minimum legal full vertical span for an orthographic camera projection.
        /// </summary>
        public const float MinimumOrthographicVerticalSpan = 0.001f;

        /// <summary>
        /// Default vertical field of view in radians, 45 degrees. Platform renderers used to
        /// choose this value individually, so the same authored scene framed differently on
        /// each platform. DirectX11, Vulkan, Dreamcast and Xbox 360 each picked 45 degrees,
        /// so this default leaves them unchanged. PlayStation 1 used 60 degrees and did
        /// reframe when it adopted the authored value, which is the divergence this removes
        /// rather than a regression.
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
        /// Creates a validated projection matrix using optional camera projection settings.
        /// Cameras that do not implement <see cref="ICameraProjectionSettings"/> retain the
        /// existing perspective projection behavior.
        /// </summary>
        /// <param name="camera">Camera providing projection and clip-plane state.</param>
        /// <param name="aspectRatio">Viewport width divided by viewport height.</param>
        /// <returns>Projection matrix matching the camera's selected projection mode.</returns>
        public static float4x4 CreateProjection(ICamera camera, float aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            ValidateAspectRatio(aspectRatio);

            ICameraProjectionSettings settings = camera as ICameraProjectionSettings;
            if (settings == null || settings.ProjectionMode == CameraProjectionMode.Perspective) {
                float4x4 perspectiveProjection = CreatePerspectiveProjection(camera, aspectRatio);
                ValidateProjectionMatrix(perspectiveProjection, nameof(aspectRatio));
                return perspectiveProjection;
            } else if (settings.ProjectionMode == CameraProjectionMode.Orthographic) {
                float span = ValidateOrthographicVerticalSpan(settings.OrthographicVerticalSpan);
                float nearPlaneDistance = ClampNearPlaneDistance(camera.NearPlaneDistance, camera.FarPlaneDistance);
                float farPlaneDistance = ClampFarPlaneDistance(nearPlaneDistance, camera.FarPlaneDistance);
                double width = (double)span * aspectRatio;
                if (!double.IsFinite(width) || width <= 0.0 || width > float.MaxValue) {
                    throw new ArgumentOutOfRangeException(nameof(aspectRatio), "The viewport aspect ratio produces an invalid orthographic width.");
                }

                float halfWidth = (float)(width * 0.5);
                float halfHeight = span * 0.5f;
                float4x4.CreateOrthographicOffCenter(-halfWidth, halfWidth, -halfHeight, halfHeight, nearPlaneDistance, farPlaneDistance, out float4x4 orthographicProjection);
                ValidateProjectionMatrix(orthographicProjection, nameof(aspectRatio));
                return orthographicProjection;
            }

            throw new ArgumentOutOfRangeException(nameof(settings.ProjectionMode), settings.ProjectionMode, "The camera projection mode is not supported.");
        }

        /// <summary>
        /// Gets the vertical world-space distance represented by one viewport pixel at a given camera distance.
        /// </summary>
        /// <param name="camera">Camera defining the active projection.</param>
        /// <param name="distance">Positive distance from the camera to the projected point.</param>
        /// <param name="viewportHeight">Positive viewport height measured in pixels.</param>
        /// <returns>Vertical world-space units represented by one pixel.</returns>
        public static double GetWorldUnitsPerPixel(ICamera camera, double distance, double viewportHeight) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            ValidatePositiveFinite(distance, nameof(distance));
            ValidatePositiveFinite(viewportHeight, nameof(viewportHeight));

            ICameraProjectionSettings settings = camera as ICameraProjectionSettings;
            if (settings != null && settings.ProjectionMode == CameraProjectionMode.Orthographic) {
                return ValidateOrthographicVerticalSpan(settings.OrthographicVerticalSpan) / viewportHeight;
            }
            if (settings != null && settings.ProjectionMode != CameraProjectionMode.Perspective) {
                throw new ArgumentOutOfRangeException(nameof(settings.ProjectionMode), "The camera projection mode is not supported.");
            }

            float fieldOfView = camera.FieldOfView;
            if (!float.IsFinite(fieldOfView) || fieldOfView <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(camera), "The camera field of view must be finite and positive.");
            }
            double clampedFieldOfView = ClampFieldOfView(fieldOfView);
            double worldHeight = 2.0 * distance * Math.Tan(clampedFieldOfView * 0.5);
            double worldUnitsPerPixel = worldHeight / viewportHeight;
            if (!double.IsFinite(worldUnitsPerPixel) || worldUnitsPerPixel <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(distance), "The requested camera scale is outside the supported numeric range.");
            }
            return worldUnitsPerPixel;
        }

        /// <summary>
        /// Validates a viewport aspect ratio before it is used to create a projection matrix.
        /// </summary>
        /// <param name="aspectRatio">Viewport width divided by viewport height.</param>
        static void ValidateAspectRatio(float aspectRatio) {
            if (!float.IsFinite(aspectRatio) || aspectRatio <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(aspectRatio), "The viewport aspect ratio must be finite and positive.");
            }
        }

        /// <summary>
        /// Validates a full vertical span before it is used to create orthographic geometry.
        /// </summary>
        /// <param name="span">Requested orthographic vertical span.</param>
        /// <returns>The validated vertical span.</returns>
        static float ValidateOrthographicVerticalSpan(float span) {
            if (!float.IsFinite(span) || span < MinimumOrthographicVerticalSpan) {
                throw new ArgumentOutOfRangeException(nameof(span), "Orthographic vertical span must be finite and at least the configured minimum.");
            }
            return span;
        }

        /// <summary>
        /// Validates a positive finite scalar used by projection-dependent calculations.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="parameterName">Public parameter name used in validation errors.</param>
        static void ValidatePositiveFinite(double value, string parameterName) {
            if (!double.IsFinite(value) || value <= 0.0) {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite and positive.");
            }
        }

        /// <summary>
        /// Ensures a generated projection contains only finite matrix values.
        /// </summary>
        /// <param name="projection">Matrix to validate.</param>
        /// <param name="parameterName">Input name associated with projection overflow.</param>
        static void ValidateProjectionMatrix(float4x4 projection, string parameterName) {
            if (!float.IsFinite(projection.M11) || !float.IsFinite(projection.M12) || !float.IsFinite(projection.M13) || !float.IsFinite(projection.M14) ||
                !float.IsFinite(projection.M21) || !float.IsFinite(projection.M22) || !float.IsFinite(projection.M23) || !float.IsFinite(projection.M24) ||
                !float.IsFinite(projection.M31) || !float.IsFinite(projection.M32) || !float.IsFinite(projection.M33) || !float.IsFinite(projection.M34) ||
                !float.IsFinite(projection.M41) || !float.IsFinite(projection.M42) || !float.IsFinite(projection.M43) || !float.IsFinite(projection.M44)) {
                throw new ArgumentOutOfRangeException(parameterName, "The projection inputs produce a non-finite matrix.");
            }
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
