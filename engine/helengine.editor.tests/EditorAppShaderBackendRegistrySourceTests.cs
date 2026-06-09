namespace helengine.editor.tests;

/// <summary>
/// Verifies the WinForms editor app host wires the shared shader backend registry into the editor session.
/// </summary>
public sealed class EditorAppShaderBackendRegistrySourceTests {
    /// <summary>
    /// Ensures the editor app host creates and passes one shader backend registry when constructing the editor session.
    /// </summary>
    [Fact]
    public void Editor_app_host_wires_shader_backend_registry_into_editor_session() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "helengine.ui",
            "helengine.editor.app",
            "MainForm.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("ShaderBackendRegistry", source, StringComparison.Ordinal);
        Assert.Contains("CreateShaderBackendRegistry", source, StringComparison.Ordinal);
        Assert.Contains("FolderDialog.OpenFolderDialog,", source, StringComparison.Ordinal);
        Assert.Contains("shaderBackendRegistry)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the helengine repository root from the current test assembly location.
    /// </summary>
    /// <returns>Absolute repository root path.</returns>
    static string ResolveRepositoryRootPath() {
        string currentPath = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentPath)) {
            string rootMarkerPath = Path.Combine(currentPath, "engine", "helengine.editor", "helengine.editor.csproj");
            if (File.Exists(rootMarkerPath)) {
                return currentPath;
            }

            DirectoryInfo parentDirectory = Directory.GetParent(currentPath);
            if (parentDirectory == null) {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        throw new InvalidOperationException("Could not resolve the helengine repository root from the current test assembly location.");
    }
}
