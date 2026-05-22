namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime content manager source keeps cooked-platform-owned runtime seams generic without registering platform-specific processors.
/// </summary>
public sealed class RuntimeContentManagerConfigurationSourceTests {
    /// <summary>
    /// Ensures the runtime content manager keeps the generic material processor registration even when platforms own opaque cooked payloads.
    /// </summary>
    [Fact]
    public void RuntimeContentManagerConfiguration_source_keeps_generic_material_processor_registration() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "content",
            "RuntimeContentManagerConfiguration.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("new AssetContentProcessor<PlatformMaterialAsset>()", source, StringComparison.Ordinal);
        Assert.Contains("new AssetContentProcessor<MaterialAsset>()", source, StringComparison.Ordinal);
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
