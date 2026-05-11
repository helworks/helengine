namespace helengine.editor {
    /// <summary>
    /// Tracks the currently hovered transform gizmo handle entity and the viewport camera that owns that hover state.
    /// </summary>
    public static class EditorGizmoHoverService {
        /// <summary>
        /// Backing field for hovered handle storage.
        /// </summary>
        static Entity HoveredHandleEntityValue;
        /// <summary>
        /// Backing field for the camera that owns the current hovered handle.
        /// </summary>
        static CameraComponent HoveredHandleCameraValue;

        /// <summary>
        /// Gets the currently hovered gizmo handle entity.
        /// </summary>
        public static Entity HoveredHandleEntity => HoveredHandleEntityValue;
        /// <summary>
        /// Gets the viewport camera that owns the current hovered handle.
        /// </summary>
        public static CameraComponent HoveredHandleCamera => HoveredHandleCameraValue;

        /// <summary>
        /// Gets the currently hovered gizmo axis entity.
        /// </summary>
        public static Entity HoveredAxisEntity => HoveredHandleEntityValue;
        /// <summary>
        /// Gets the hovered gizmo handle when it belongs to the specified camera or is not camera-scoped.
        /// </summary>
        /// <param name="camera">Viewport camera requesting hover state.</param>
        /// <returns>Hovered handle that is visible to the specified camera; otherwise null.</returns>
        public static Entity GetHoveredHandle(CameraComponent camera) {
            if (HoveredHandleEntityValue == null) {
                return null;
            }
            if (HoveredHandleCameraValue == null || ReferenceEquals(HoveredHandleCameraValue, camera)) {
                return HoveredHandleEntityValue;
            }

            return null;
        }

        /// <summary>
        /// Gets the hovered gizmo axis when it belongs to the specified camera or is not camera-scoped.
        /// </summary>
        /// <param name="camera">Viewport camera requesting hover state.</param>
        /// <returns>Hovered axis that is visible to the specified camera; otherwise null.</returns>
        public static Entity GetHoveredAxis(CameraComponent camera) {
            return GetHoveredHandle(camera);
        }

        /// <summary>
        /// Sets the currently hovered gizmo handle entity without camera ownership.
        /// </summary>
        /// <param name="handleEntity">Handle entity to set, or null to clear hover.</param>
        public static void SetHoveredHandle(Entity handleEntity) {
            HoveredHandleEntityValue = handleEntity;
            HoveredHandleCameraValue = null;
        }

        /// <summary>
        /// Sets the currently hovered gizmo handle entity for a specific viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera that owns the hovered handle.</param>
        /// <param name="handleEntity">Handle entity to set, or null to clear hover.</param>
        public static void SetHoveredHandle(CameraComponent camera, Entity handleEntity) {
            HoveredHandleEntityValue = handleEntity;
            HoveredHandleCameraValue = handleEntity == null ? null : camera;
        }

        /// <summary>
        /// Sets the currently hovered gizmo axis entity without camera ownership.
        /// </summary>
        /// <param name="axisEntity">Axis entity to set, or null to clear hover.</param>
        public static void SetHoveredAxis(Entity axisEntity) {
            SetHoveredHandle(axisEntity);
        }

        /// <summary>
        /// Sets the currently hovered gizmo axis entity for a specific viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera that owns the hovered axis.</param>
        /// <param name="axisEntity">Axis entity to set, or null to clear hover.</param>
        public static void SetHoveredAxis(CameraComponent camera, Entity axisEntity) {
            SetHoveredHandle(camera, axisEntity);
        }

        /// <summary>
        /// Clears the current hovered gizmo handle regardless of ownership.
        /// </summary>
        public static void ClearHoveredHandle() {
            HoveredHandleEntityValue = null;
            HoveredHandleCameraValue = null;
        }

        /// <summary>
        /// Clears the current hovered gizmo handle when it belongs to the specified viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera requesting the clear.</param>
        public static void ClearHoveredHandle(CameraComponent camera) {
            if (HoveredHandleCameraValue == null || !ReferenceEquals(HoveredHandleCameraValue, camera)) {
                return;
            }

            ClearHoveredHandle();
        }

        /// <summary>
        /// Clears the current hovered gizmo axis regardless of ownership.
        /// </summary>
        public static void ClearHoveredAxis() {
            ClearHoveredHandle();
        }

        /// <summary>
        /// Clears the current hovered gizmo axis when it belongs to the specified viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera requesting the clear.</param>
        public static void ClearHoveredAxis(CameraComponent camera) {
            ClearHoveredHandle(camera);
        }
    }
}
