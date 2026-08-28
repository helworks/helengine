namespace helengine.editor.tests;

/// <summary>
/// Verifies viewport workspace teardown clears static gizmo state before disposing viewport-owned gizmo entities and cameras.
/// </summary>
public sealed class ViewportWorkspacePanelControllerSourceTests {
    /// <summary>
    /// Ensures viewport disposal invalidates hovered-handle and drag state before runtime camera stack disposal begins.
    /// </summary>
    [Fact]
    public void Dispose_source_clears_gizmo_state_before_disposing_viewport_entities() {
        string sourcePath = ResolveRepositorySourcePath(
            "engine",
            "helengine.editor",
            "managers",
            "workspace",
            "ViewportWorkspacePanelController.cs");
        string source = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "State.Viewport.ClearInputBlockers();\n            EditorGizmoHoverService.ClearHoveredHandle(State.SceneCamera);\n            EditorGizmoDragService.EndDrag(State.SceneCamera);\n            EditorViewportToolService.ClearToolMode(State.SceneCamera);\n            TransformGizmoSnapSettingsService.ClearState(State.SceneCamera);\n            State.TranslationGizmoRoot.Dispose();",
            source,
            StringComparison.Ordinal);
    }

    static string ResolveRepositorySourcePath(params string[] relativeSegments) {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            string candidate = current.FullName;
            for (int index = 0; index < relativeSegments.Length; index++) {
                candidate = Path.Combine(candidate, relativeSegments[index]);
            }

            if (File.Exists(candidate)) {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not resolve the viewport workspace panel controller source from the test assembly location.",
            string.Join(Path.DirectorySeparatorChar, relativeSegments));
    }
}
