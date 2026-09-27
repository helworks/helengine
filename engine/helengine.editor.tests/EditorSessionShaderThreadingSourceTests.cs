namespace helengine.editor.tests;

/// <summary>
/// Verifies shader hot-reload notifications stay on the editor frame thread before mutating DirectX runtime shader resources.
/// </summary>
public sealed class EditorSessionShaderThreadingSourceTests {

    static string ResolveCurrentWorktreeSource(string projectDirectoryName, string fileName) {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            string candidate = Path.Combine(current.FullName, projectDirectoryName, fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{projectDirectoryName}/{fileName}' from the current test worktree.");
    }
}
