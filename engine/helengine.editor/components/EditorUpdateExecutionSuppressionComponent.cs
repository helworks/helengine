namespace helengine {
    /// <summary>
    /// Marks one editor-authored entity so gameplay update components stay inactive while the editor is authoring the scene.
    /// </summary>
    public class EditorUpdateExecutionSuppressionComponent : Component, IEditorHiddenComponent {
    }
}
