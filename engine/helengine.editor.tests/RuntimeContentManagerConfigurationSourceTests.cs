namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime content manager source keeps the cooked-platform-owned material processor registration aligned with the generic runtime material loader.
/// </summary>
public sealed class RuntimeContentManagerConfigurationSourceTests {
    /// <summary>
    /// Ensures the runtime content manager source branches on the generic cooked-platform-owned material contract symbol.
    /// </summary>
    [Fact]
    public void RuntimeContentManagerConfiguration_source_registers_generic_material_processor_for_cooked_platform_owned_material_contract() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "content",
            "RuntimeContentManagerConfiguration.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("new AssetContentProcessor<PlatformMaterialAsset>()", source, StringComparison.Ordinal);
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
