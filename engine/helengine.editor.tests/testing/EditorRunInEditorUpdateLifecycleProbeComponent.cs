namespace helengine.editor.tests.testing {
    /// <summary>
    /// Probe update component that explicitly opts into running inside the editor.
    /// </summary>
    [RunInEditor]
    class EditorRunInEditorUpdateLifecycleProbeComponent : EditorUpdateLifecycleProbeComponent {
    }
}
