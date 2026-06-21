namespace helengine {
    /// <summary>
    /// Marks one scene camera entity as editor-suppressed so the live camera can stay authored without participating in editor runtime rendering.
    /// </summary>
    public class EditorSceneCameraSuppressionComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Initializes one hidden editor suppression marker.
        /// </summary>
        public EditorSceneCameraSuppressionComponent() {
        }
    }
}
