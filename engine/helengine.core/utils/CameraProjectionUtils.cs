namespace helengine {
    /// <summary>
    /// Builds validated perspective and orthographic projections from camera state.
    /// </summary>
    public static class CameraProjectionUtils {
        /// <summary>
        /// Minimum legal near clip-plane distance used by validated perspective projections.
        /// </summary>
        public static readonly Real MinimumNearPlaneDistance = 0.01f;

        /// <summary>
        /// Minimum legal separation preserved between the near and far clip planes.
        /// </summary>
        public static readonly Real MinimumPlaneSeparation = 0.01f;

        /// <summary>
        /// Minimum legal full vertical span for an orthographic camera projection.
        /// </summary>
        public static readonly Real MinimumOrthographicVerticalSpan = 0.001f;

        /// <summary>
        /// Default vertical field of view in radians, 45 degrees. Platform renderers used to
        /// choose this value individually, so the same authored scene framed differently on
        /// each platform. DirectX11, Vulkan, Dreamcast and Xbox 360 each picked 45 degrees,
        /// so this default leaves them unchanged. PlayStation 1 used 60 degrees and did
        /// reframe when it adopted the authored value, which is the divergence this removes
        /// rather than a regression.
        /// </summary>
        public static readonly Real DefaultFieldOfView = 0.7853982f;

        /// <summary>
        /// Minimum legal vertical field of view in radians, one degree.
        /// </summary>
        public static readonly Real MinimumFieldOfView = 0.0174533f;

        /// <summary>
        /// Maximum legal vertical field of view in radians, 175 degrees. The projection
        /// builder rejects a field of view of pi or wider.
        /// </summary>
        public static readonly Real MaximumFieldOfView = 3.0543262f;

        /// <summary>
        /// Clamps one vertical field of view into the range the projection builder accepts.
        /// </summary>
        /// <param name="fieldOfView">Requested vertical field of view in radians.</param>
        /// <returns>Legal vertical field of view in radians.</returns>
        public static RealEvaluation ClampFieldOfView(RealEvaluation fieldOfView) {
            return RealMath.Min(RealMath.Max(MinimumFieldOfView, fieldOfView), MaximumFieldOfView);
        }

        /// <summary>
        /// Clamps one near clip-plane distance against the current far clip plane.
        /// </summary>
        /// <param name="nearPlaneDistance">Requested near clip-plane distance.</param>
        /// <param name="farPlaneDistance">Current far clip-plane distance.</param>
        /// <returns>Legal near clip-plane distance for the provided projection state.</returns>
        public static RealEvaluation ClampNearPlaneDistance(RealEvaluation nearPlaneDistance, RealEvaluation farPlaneDistance) {
            RealEvaluation minimumFarPlaneDistance = ClampFarPlaneDistance(MinimumNearPlaneDistance, farPlaneDistance);
            return RealMath.Min(RealMath.Max(MinimumNearPlaneDistance, nearPlaneDistance), minimumFarPlaneDistance - MinimumPlaneSeparation);
        }

        /// <summary>
        /// Clamps one far clip-plane distance against the current near clip plane.
        /// </summary>
        /// <param name="nearPlaneDistance">Current near clip-plane distance.</param>
        /// <param name="farPlaneDistance">Requested far clip-plane distance.</param>
        /// <returns>Legal far clip-plane distance for the provided projection state.</returns>
        public static RealEvaluation ClampFarPlaneDistance(RealEvaluation nearPlaneDistance, RealEvaluation farPlaneDistance) {
            RealEvaluation minimumNearPlaneDistance = RealMath.Max(MinimumNearPlaneDistance, nearPlaneDistance);
            return RealMath.Max(minimumNearPlaneDistance + MinimumPlaneSeparation, farPlaneDistance);
        }

        /// <summary>
        /// Creates a validated projection matrix using optional camera projection settings.
        /// Cameras that do not implement <see cref="ICameraProjectionSettings"/> retain the
        /// existing perspective projection behavior.
        /// </summary>
        /// <param name="camera">Camera providing projection and clip-plane state.</param>
        /// <param name="aspectRatio">Viewport width divided by viewport height.</param>
        /// <returns>Projection matrix matching the camera's selected projection mode.</returns>
        public static float4x4 CreateProjection(ICamera camera, RealEvaluation aspectRatio) {
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
                RealEvaluation span = ValidateOrthographicVerticalSpan(settings.OrthographicVerticalSpan);
                RealEvaluation nearPlaneDistance = ClampNearPlaneDistance(camera.NearPlaneDistance, camera.FarPlaneDistance);
                RealEvaluation farPlaneDistance = ClampFarPlaneDistance(nearPlaneDistance, camera.FarPlaneDistance);
                RealEvaluation width = (RealEvaluation)span * aspectRatio;
                if (!RealEvaluation.IsFinite(width) || width <= 0.0 || width > RealEvaluation.MaxValue) {
                    throw new ArgumentOutOfRangeException(nameof(aspectRatio), "The viewport aspect ratio produces an invalid orthographic width.");
                }

                RealEvaluation halfWidth = (RealEvaluation)(width * 0.5);
                RealEvaluation halfHeight = span * 0.5f;
                float4x4.CreateOrthographicOffCenter(-halfWidth, halfWidth, -halfHeight, halfHeight, nearPlaneDistance, farPlaneDistance, out float4x4 orthographicProjection);
                ValidateProjectionMatrix(orthographicProjection, nameof(aspectRatio));
                return orthographicProjection;
            }

            throw new ArgumentOutOfRangeException(nameof(settings.ProjectionMode), settings.ProjectionMode, "The camera projection mode is not supported.");
        }

        /// <summary>
        /// Resolves the projection aspect ratio of a viewport on a display whose pixels may not be square: the viewport's
        /// displayed width divided by its displayed height, <c>viewportWidth * pixelAspect / viewportHeight</c>. With square
        /// pixels it is exactly <c>viewportWidth / viewportHeight</c> in single precision, as every renderer computed it
        /// before, so square-pixel platforms project bit for bit as they did.
        /// </summary>
        /// <param name="viewportWidth">Viewport width in framebuffer pixels.</param>
        /// <param name="viewportHeight">Viewport height in framebuffer pixels.</param>
        /// <param name="pixelAspect">Displayed width of one framebuffer pixel divided by its displayed height.</param>
        /// <returns>The aspect ratio to build the projection with.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-finite or non-positive input, or an aspect outside single precision.</exception>
        public static RealEvaluation ResolveProjectionAspect(RealEvaluation viewportWidth, RealEvaluation viewportHeight, RealEvaluation pixelAspect) {
            if (!RealEvaluation.IsFinite(viewportWidth) || viewportWidth <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(viewportWidth), "The viewport width must be finite and positive.");
            } else if (!RealEvaluation.IsFinite(viewportHeight) || viewportHeight <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(viewportHeight), "The viewport height must be finite and positive.");
            } else if (!RealEvaluation.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), "The pixel aspect must be finite and positive.");
            } else if (pixelAspect == RenderManager3D.SquarePixelAspect) {
                return viewportWidth / viewportHeight;
            }

            RealEvaluation aspect = viewportWidth * pixelAspect / viewportHeight;
            if (!RealEvaluation.IsFinite(aspect) || aspect > RealEvaluation.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), "The viewport and pixel aspect produce an aspect ratio outside single precision.");
            }
            return (RealEvaluation)aspect;
        }

        /// <summary>
        /// Gets the vertical world-space distance represented by one viewport pixel at a given camera distance.
        /// </summary>
        /// <param name="camera">Camera defining the active projection.</param>
        /// <param name="distance">Positive distance from the camera to the projected point.</param>
        /// <param name="viewportHeight">Positive viewport height measured in pixels.</param>
        /// <returns>Vertical world-space units represented by one pixel.</returns>
        public static RealEvaluation GetWorldUnitsPerPixel(ICamera camera, RealEvaluation distance, RealEvaluation viewportHeight) {
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

            RealEvaluation fieldOfView = camera.FieldOfView;
            if (!RealEvaluation.IsFinite(fieldOfView) || fieldOfView <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(camera), "The camera field of view must be finite and positive.");
            }
            RealEvaluation clampedFieldOfView = ClampFieldOfView(fieldOfView);
            RealEvaluation worldHeight = 2.0 * distance * RealMath.Tan(clampedFieldOfView * 0.5);
            RealEvaluation worldUnitsPerPixel = worldHeight / viewportHeight;
            if (!RealEvaluation.IsFinite(worldUnitsPerPixel) || worldUnitsPerPixel <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(distance), "The requested camera scale is outside the supported numeric range.");
            }
            return worldUnitsPerPixel;
        }

        /// <summary>
        /// Validates a viewport aspect ratio before it is used to create a projection matrix.
        /// </summary>
        /// <param name="aspectRatio">Viewport width divided by viewport height.</param>
        static void ValidateAspectRatio(RealEvaluation aspectRatio) {
            if (!RealEvaluation.IsFinite(aspectRatio) || aspectRatio <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(aspectRatio), "The viewport aspect ratio must be finite and positive.");
            }
        }

        /// <summary>
        /// Validates a full vertical span before it is used to create orthographic geometry.
        /// </summary>
        /// <param name="span">Requested orthographic vertical span.</param>
        /// <returns>The validated vertical span.</returns>
        static RealEvaluation ValidateOrthographicVerticalSpan(RealEvaluation span) {
            if (!RealEvaluation.IsFinite(span) || span < MinimumOrthographicVerticalSpan) {
                throw new ArgumentOutOfRangeException(nameof(span), "Orthographic vertical span must be finite and at least the configured minimum.");
            }
            return span;
        }

        /// <summary>
        /// Validates a positive finite scalar used by projection-dependent calculations.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="parameterName">Public parameter name used in validation errors.</param>
        static void ValidatePositiveFinite(RealEvaluation value, string parameterName) {
            if (!RealEvaluation.IsFinite(value) || value <= 0.0) {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite and positive.");
            }
        }

        /// <summary>
        /// Ensures a generated projection contains only finite matrix values.
        /// </summary>
        /// <param name="projection">Matrix to validate.</param>
        /// <param name="parameterName">Input name associated with projection overflow.</param>
        static void ValidateProjectionMatrix(float4x4 projection, string parameterName) {
            if (!RealEvaluation.IsFinite(projection.M11) || !RealEvaluation.IsFinite(projection.M12) || !RealEvaluation.IsFinite(projection.M13) || !RealEvaluation.IsFinite(projection.M14) ||
                !RealEvaluation.IsFinite(projection.M21) || !RealEvaluation.IsFinite(projection.M22) || !RealEvaluation.IsFinite(projection.M23) || !RealEvaluation.IsFinite(projection.M24) ||
                !RealEvaluation.IsFinite(projection.M31) || !RealEvaluation.IsFinite(projection.M32) || !RealEvaluation.IsFinite(projection.M33) || !RealEvaluation.IsFinite(projection.M34) ||
                !RealEvaluation.IsFinite(projection.M41) || !RealEvaluation.IsFinite(projection.M42) || !RealEvaluation.IsFinite(projection.M43) || !RealEvaluation.IsFinite(projection.M44)) {
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
        public static float4x4 CreatePerspectiveProjection(ICamera camera, RealEvaluation aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return CreatePerspectiveProjection(camera, camera.FieldOfView, aspectRatio);
        }

        public static float4x4 CreatePerspectiveProjection(ICamera camera, RealEvaluation fieldOfView, RealEvaluation aspectRatio) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            RealEvaluation nearPlaneDistance = ClampNearPlaneDistance(camera.NearPlaneDistance, camera.FarPlaneDistance);
            RealEvaluation farPlaneDistance = ClampFarPlaneDistance(nearPlaneDistance, camera.FarPlaneDistance);
            float4x4.CreatePerspectiveFieldOfView(ClampFieldOfView(fieldOfView), aspectRatio, nearPlaneDistance, farPlaneDistance, out float4x4 projection);
            return projection;
        }
    }
}
