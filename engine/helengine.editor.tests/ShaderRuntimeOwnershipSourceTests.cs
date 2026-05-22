namespace helengine.editor.tests;

/// <summary>
/// Verifies shader runtime ownership no longer belongs to helengine.core.
/// </summary>
public sealed class ShaderRuntimeOwnershipSourceTests {
    /// <summary>
    /// Ensures the shader runtime source tree is not owned by helengine.core.
    /// </summary>
    [Fact]
    public void Shader_runtime_metadata_is_not_owned_by_core() {
        string repositoryRootPath = ResolveRepositoryRootPath();
        string coreShaderRootPath = Path.Combine(repositoryRootPath, "engine", "helengine.core", "shaders");
        string shaderShaderRootPath = Path.Combine(repositoryRootPath, "engine", "helengine.shader", "shaders");

        Assert.False(Directory.Exists(coreShaderRootPath));
        Assert.True(Directory.Exists(shaderShaderRootPath));
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
