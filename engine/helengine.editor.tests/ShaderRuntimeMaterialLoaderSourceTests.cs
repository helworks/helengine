namespace helengine.editor.tests;

/// <summary>
/// Verifies the shader runtime material loader follows the shared cooked-texture runtime-generation symbol contract.
/// </summary>
public sealed class ShaderRuntimeMaterialLoaderSourceTests {
    /// <summary>
    /// Ensures imported diffuse textures on packaged shader-backed materials use the shared cooked-texture resolution symbol when the runtime platform owns texture payload creation.
    /// </summary>
    [Fact]
    public void ShaderRuntimeMaterialLoader_source_uses_generic_cooked_texture_resolution_symbol() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.shader",
            "assets",
            "ShaderRuntimeMaterialLoader.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_TEXTURE_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("BuildTextureFromCooked(diffuseTexturePath)", source, StringComparison.Ordinal);
        Assert.Contains("BuildTextureFromRaw(textureAsset)", source, StringComparison.Ordinal);
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
