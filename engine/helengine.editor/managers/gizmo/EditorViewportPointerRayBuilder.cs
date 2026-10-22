namespace helengine.editor {
    /// <summary>
    /// Builds world-space pointer rays for scene-camera viewport interactions.
    /// </summary>
    public static class EditorViewportPointerRayBuilder {
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
            return RotationGizmoMath.TryBuildCameraRay(camera, viewport, pointer, useCameraProjection, out rayOrigin, out rayDirection);
        }
    }
}
