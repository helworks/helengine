namespace helengine.editor {
    /// <summary>
    /// Stores gizmo tool selection state per viewport camera.
    /// </summary>
    public sealed class EditorViewportToolService : IDisposable {
        /// <summary>
        /// Tool mode mapped by viewport camera instance.
        /// </summary>
        readonly Dictionary<CameraComponent, EditorViewportToolMode> ToolModesByCamera =
            new Dictionary<CameraComponent, EditorViewportToolMode>();

        /// <summary>
        /// Default tool mode used when no mode was explicitly assigned to a camera.
        /// </summary>
        public const EditorViewportToolMode DefaultToolMode = EditorViewportToolMode.Translate;

        /// <summary>
        /// Assigns a tool mode for a viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera receiving the tool mode.</param>
        /// <param name="toolMode">Tool mode to assign.</param>
        public void SetToolMode(CameraComponent camera, EditorViewportToolMode toolMode) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            ToolModesByCamera[camera] = toolMode;
        }

        /// <summary>
        /// Reads the current tool mode for a viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera to query.</param>
        /// <returns>Assigned tool mode, or the default mode when unassigned.</returns>
        public EditorViewportToolMode GetToolMode(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            if (ToolModesByCamera.TryGetValue(camera, out EditorViewportToolMode toolMode)) {
                return toolMode;
            }

            return DefaultToolMode;
        }

        /// <summary>
        /// Removes any stored tool mode for a camera, causing it to use the default mode.
        /// </summary>
        /// <param name="camera">Viewport camera whose tool mode mapping should be cleared.</param>
        public void ClearToolMode(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            ToolModesByCamera.Remove(camera);
        }

        /// <summary>
        /// Clears all per-viewport tool selections owned by an editor session.
        /// </summary>
        public void Dispose() {
            ToolModesByCamera.Clear();
        }
    }
}
