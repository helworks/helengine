namespace helengine.editor {
    /// <summary>
    /// Builds world-space pointer rays for scene-camera viewport interactions.
    /// </summary>
    public static class EditorViewportPointerRayBuilder {
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero during normalization.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;
        /// <summary>
        /// Minimum viewport dimension accepted when mapping a pointer into a camera ray.
        /// </summary>
        const double MinimumViewportDimension = 1.0;
        /// <summary>
        /// Forward axis used by cameras before orientation is applied.
        /// </summary>
        static readonly float3 CameraForwardAxis = new float3(0f, 0f, -1f);

        /// <summary>
        /// Builds a normalized world-space ray using the camera's active perspective or orthographic projection.
        /// </summary>
        /// <param name="camera">Camera used to convert the pointer into a world-space ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="rayOrigin">Resolved ray origin in world space.</param>
        /// <param name="rayDirection">Resolved normalized ray direction in world space.</param>
        /// <returns>True when the camera ray can be constructed.</returns>
        public static bool TryBuildCameraRay(CameraComponent camera, int2 pointer, out float3 rayOrigin, out float3 rayDirection) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return TryBuildCameraRay(camera, camera.Viewport, pointer, true, out rayOrigin, out rayDirection);
        }

        /// <summary>
        /// Builds a ray from a camera using the viewport rectangle captured for an editor pointer request.
        /// </summary>
        /// <param name="camera">Camera used to build the ray.</param>
        /// <param name="viewport">Viewport rectangle captured with the pointer request.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="rayOrigin">Resolved ray origin.</param>
        /// <param name="rayDirection">Resolved normalized ray direction.</param>
        /// <returns>True when the ray can be constructed.</returns>
        internal static bool TryBuildCameraRay(CameraComponent camera, float4 viewport, int2 pointer, out float3 rayOrigin, out float3 rayDirection) {
            return TryBuildCameraRay(camera, viewport, pointer, true, out rayOrigin, out rayDirection);
        }

        /// <summary>
        /// Builds a normalized world-space perspective ray, preserving compatibility for perspective-only callers.
        /// </summary>
        /// <param name="sceneCamera">Perspective camera used to convert the pointer into a world-space ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="rayOrigin">Resolved ray origin in world space.</param>
        /// <param name="rayDirection">Resolved normalized ray direction in world space.</param>
        /// <returns>True when the camera ray can be constructed.</returns>
        public static bool TryBuildPerspectiveCameraRay(CameraComponent sceneCamera, int2 pointer, out float3 rayOrigin, out float3 rayDirection) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            return TryBuildCameraRay(sceneCamera, sceneCamera.Viewport, pointer, false, out rayOrigin, out rayDirection);
        }

        /// <summary>
        /// Builds one ray from camera and viewport state, optionally honoring orthographic settings.
        /// </summary>
        /// <param name="camera">Camera used to build the ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="useCameraProjection">True to use optional projection settings; false to force perspective.</param>
        /// <param name="rayOrigin">Resolved ray origin.</param>
        /// <param name="rayDirection">Resolved normalized ray direction.</param>
        /// <returns>True when the ray can be constructed.</returns>
        static bool TryBuildCameraRay(CameraComponent camera, float4 viewport, int2 pointer, bool useCameraProjection, out float3 rayOrigin, out float3 rayDirection) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            Entity cameraEntity = camera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            if (!float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) ||
                !float.IsFinite(viewport.Z) || !float.IsFinite(viewport.W) ||
                viewport.Z <= MinimumViewportDimension || viewport.W <= MinimumViewportDimension) {
                rayOrigin = float3.Zero;
                rayDirection = float3.Zero;
                return false;
            }

            double normalizedX = (pointer.X - viewport.X) / viewport.Z;
            double normalizedY = (pointer.Y - viewport.Y) / viewport.W;
            double ndcX = (normalizedX * 2.0) - 1.0;
            double ndcY = 1.0 - (normalizedY * 2.0);
            double aspect = viewport.Z / viewport.W;
            if (!double.IsFinite(aspect) || aspect <= 0.0) {
                rayOrigin = float3.Zero;
                rayDirection = float3.Zero;
                return false;
            }

            float4 cameraOrientation = cameraEntity.Orientation;
            float3 worldForward = NormalizeSafe(float4.RotateVector(CameraForwardAxis, cameraOrientation), CameraForwardAxis);
            ICameraProjectionSettings projectionSettings = useCameraProjection ? camera as ICameraProjectionSettings : null;
            if (projectionSettings != null && projectionSettings.ProjectionMode == CameraProjectionMode.Orthographic) {
                double span = projectionSettings.OrthographicVerticalSpan;
                if (!double.IsFinite(span) || span < CameraProjectionUtils.MinimumOrthographicVerticalSpan) {
                    rayOrigin = float3.Zero;
                    rayDirection = float3.Zero;
                    return false;
                }

                float3 cameraRight = NormalizeSafe(float4.RotateVector(new float3(1f, 0f, 0f), cameraOrientation), new float3(1f, 0f, 0f));
                float3 cameraUp = NormalizeSafe(float4.RotateVector(new float3(0f, 1f, 0f), cameraOrientation), new float3(0f, 1f, 0f));
                double horizontalOffset = ndcX * span * aspect * 0.5;
                double verticalOffset = ndcY * span * 0.5;
                rayOrigin = cameraEntity.Position + (cameraRight * (float)horizontalOffset) + (cameraUp * (float)verticalOffset);
                rayDirection = worldForward;
                return true;
            }

            double fieldOfView = CameraProjectionUtils.ClampFieldOfView(camera.FieldOfView);
            double tanHalfFieldOfView = Math.Tan(fieldOfView * 0.5);
            float3 cameraSpaceDirection = new float3(
                (float)(ndcX * aspect * tanHalfFieldOfView),
                (float)(ndcY * tanHalfFieldOfView),
                -1f);
            cameraSpaceDirection = NormalizeSafe(cameraSpaceDirection, CameraForwardAxis);
            rayDirection = NormalizeSafe(float4.RotateVector(cameraSpaceDirection, cameraOrientation), worldForward);
            rayOrigin = cameraEntity.Position;
            return rayDirection != float3.Zero;
        }

        /// <summary>
        /// Normalizes a vector or returns a fallback when the magnitude is too small.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <param name="fallback">Fallback direction returned for near-zero vectors.</param>
        /// <returns>Normalized vector when valid; otherwise the fallback value.</returns>
        static float3 NormalizeSafe(float3 value, float3 fallback) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= MinimumVectorLengthSquared) {
                return fallback;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }
    }
}
