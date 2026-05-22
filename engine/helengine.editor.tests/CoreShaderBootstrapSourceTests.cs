namespace helengine.editor.tests;

/// <summary>
/// Verifies generic core bootstrap does not own shader target selection.
/// </summary>
public sealed class CoreShaderBootstrapSourceTests {
    /// <summary>
    /// Ensures core bootstrap does not reference shader compile target selection directly.
    /// </summary>
    [Fact]
    public void Core_does_not_own_shader_target_selection() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "Core.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("ShaderCompileTarget", source, StringComparison.Ordinal);
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
