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

        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that keeps one authored scene camera out of the runtime camera list during scene authoring.
        /// </summary>
        public override bool IsEditorSceneCameraSuppressionMarker => true;
    }
}
