namespace helengine {
    /// <summary>
    /// Marks one editor-authored entity so gameplay update components stay inactive while the editor is authoring the scene.
    /// </summary>
    public class EditorUpdateExecutionSuppressionComponent : Component, IEditorHiddenComponent {
        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that disables gameplay update execution during scene authoring.
        /// </summary>
        public override bool IsEditorUpdateExecutionSuppressionMarker => true;
    }
}
