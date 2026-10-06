namespace helengine.editor.tests;

/// <summary>
/// Verifies viewport workspace teardown clears static gizmo state before disposing viewport-owned gizmo entities and cameras.
/// </summary>
public sealed class ViewportWorkspacePanelControllerSourceTests {

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
