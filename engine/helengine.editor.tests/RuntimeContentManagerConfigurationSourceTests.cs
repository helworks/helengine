namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime content manager source keeps the PS2 material processor registration aligned with the PS2 runtime material loader.
/// </summary>
public sealed class RuntimeContentManagerConfigurationSourceTests {
    /// <summary>
    /// Ensures the PS2 runtime branch registers the material processor for <see cref="Ps2MaterialAsset"/> instead of the desktop material asset type.
    /// </summary>
    [Fact]
    public void RuntimeContentManagerConfiguration_source_registers_ps2_material_processor_for_ps2_runtime_branch() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "content",
            "RuntimeContentManagerConfiguration.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if PS2_PLATFORM", source, StringComparison.Ordinal);
        Assert.Contains("new AssetContentProcessor<Ps2MaterialAsset>()", source, StringComparison.Ordinal);
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
