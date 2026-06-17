namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime content manager source exposes the generated material-registration seams required by both generic and cooked-platform-owned runtimes.
/// </summary>
public sealed class RuntimeContentManagerConfigurationSourceTests {
    /// <summary>
    /// Ensures the runtime content manager branches on the generated runtime material-resolution contract instead of hardcoding only the generic material processor registration.
    /// </summary>
    [Fact]
    public void RuntimeContentManagerConfiguration_source_branches_between_generic_and_cooked_platform_material_processor_registration() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "content",
            "RuntimeContentManagerConfiguration.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("new AssetContentProcessor<PlatformMaterialAsset>()", source, StringComparison.Ordinal);
        Assert.Contains("new AssetContentProcessor<MaterialAsset>()", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the core runtime content manager no longer hardcodes shader package registration details.
    /// </summary>
    [Fact]
    public void RuntimeContentManagerConfiguration_source_does_not_register_shader_packages() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "content",
            "RuntimeContentManagerConfiguration.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("runtime.shader-asset", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".shader.asset", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new AssetContentProcessor<ShaderAsset>()", source, StringComparison.Ordinal);
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
