namespace helengine.editor {
    /// <summary>
    /// Builds world-space pointer rays for scene-camera viewport interactions.
    /// </summary>
    public static class EditorViewportPointerRayBuilder {
        /// <summary>
        /// Perspective vertical field of view used by scene camera rendering.
        /// </summary>
        const double PerspectiveVerticalFieldOfViewRadians = Math.PI / 4.0;
        /// <summary>
        /// Smallest squared vector magnitude treated as non-zero during normalization.
        /// </summary>
        const double MinimumVectorLengthSquared = 0.000000000001;
        /// <summary>
        /// Forward axis used by cameras before orientation is applied.
        /// </summary>
        static readonly float3 CameraForwardAxis = new float3(0f, 0f, -1f);

        /// <summary>
        /// Builds a normalized world-space camera ray from a pointer position relative to the supplied scene camera viewport.
        /// </summary>
        /// <param name="sceneCamera">Scene camera used to convert the pointer into a world-space ray.</param>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <param name="rayOrigin">Resolved ray origin in world space.</param>
        /// <param name="rayDirection">Resolved ray direction in world space.</param>
        /// <returns>True when the camera ray can be constructed.</returns>
        public static bool TryBuildPerspectiveCameraRay(
            CameraComponent sceneCamera,
            int2 pointer,
            out float3 rayOrigin,
            out float3 rayDirection) {
            if (sceneCamera == null) {
                throw new ArgumentNullException(nameof(sceneCamera));
            }

            Entity cameraEntity = sceneCamera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Scene camera must belong to an entity.");
            }

            float4 viewport = sceneCamera.Viewport;
            if (viewport.Z <= 1f || viewport.W <= 1f) {
                rayOrigin = float3.Zero;
                rayDirection = float3.Zero;
                return false;
            }

            double normalizedX = (pointer.X - viewport.X) / viewport.Z;
            double normalizedY = (pointer.Y - viewport.Y) / viewport.W;
            double ndcX = (normalizedX * 2.0) - 1.0;
            double ndcY = 1.0 - (normalizedY * 2.0);
            double aspect = viewport.Z / viewport.W;
            if (aspect <= 0.0) {
                throw new InvalidOperationException("Scene camera viewport aspect ratio must be positive.");
            }

            double tanHalfFov = Math.Tan(PerspectiveVerticalFieldOfViewRadians * 0.5);
            float3 cameraSpaceDirection = new float3(
                (float)(ndcX * aspect * tanHalfFov),
                (float)(ndcY * tanHalfFov),
                -1f);
            cameraSpaceDirection = NormalizeSafe(cameraSpaceDirection, CameraForwardAxis);

            float4 cameraOrientation = cameraEntity.Orientation;
            float3 forwardFallback = float4.RotateVector(CameraForwardAxis, cameraOrientation);
            rayDirection = NormalizeSafe(float4.RotateVector(cameraSpaceDirection, cameraOrientation), forwardFallback);
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
