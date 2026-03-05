namespace helengine.editor {
    /// <summary>
    /// Stores active transform-gizmo drag state per viewport camera.
    /// </summary>
    public static class EditorGizmoDragService {
        /// <summary>
        /// Active dragged entity mapped by viewport camera.
        /// </summary>
        static readonly Dictionary<CameraComponent, Entity> DraggedEntityByCamera =
            new Dictionary<CameraComponent, Entity>();

        /// <summary>
        /// Registers an active drag for a viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera driving the drag operation.</param>
        /// <param name="draggedEntity">Entity currently being transformed.</param>
        public static void BeginDrag(CameraComponent camera, Entity draggedEntity) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            if (draggedEntity == null) {
                throw new ArgumentNullException(nameof(draggedEntity));
            }

            DraggedEntityByCamera[camera] = draggedEntity;
        }

        /// <summary>
        /// Clears the active drag registration for a viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera whose drag registration should be removed.</param>
        public static void EndDrag(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            DraggedEntityByCamera.Remove(camera);
        }

        /// <summary>
        /// Determines whether the provided viewport camera currently has an active gizmo drag.
        /// </summary>
        /// <param name="camera">Viewport camera to query.</param>
        /// <returns>True when the camera has an active drag registration; otherwise false.</returns>
        public static bool IsDragging(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return DraggedEntityByCamera.ContainsKey(camera);
        }
    }
}
