namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene-asset resolver source branches on generic runtime-generation symbols instead of platform-name-specific symbols.
/// </summary>
public sealed class RuntimeSceneAssetReferenceResolverSourceTests {
    /// <summary>
    /// Ensures cooked platform-owned material resolution uses the shared runtime-generation symbol.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_uses_generic_cooked_material_resolution_symbol() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED", source, StringComparison.Ordinal);
        Assert.Contains("PlatformMaterialAsset materialAsset = AssetContentManager.Load<PlatformMaterialAsset>(fullPath, RuntimeContentProcessorIds.MaterialAsset);", source, StringComparison.Ordinal);
        Assert.Contains("BuildMaterialFromCooked(materialAsset)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures rooted packaged-path allowance uses the shared runtime-generation symbol.
    /// </summary>
    [Fact]
    public void RuntimeSceneAssetReferenceResolver_source_uses_generic_rooted_packaged_path_symbol() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "scene",
            "runtime",
            "RuntimeSceneAssetReferenceResolver.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("#if HELENGINE_RUNTIME_ALLOW_ROOTED_PACKAGED_PATHS", source, StringComparison.Ordinal);
        Assert.Contains("if (Path.IsPathRooted(reference.RelativePath))", source, StringComparison.Ordinal);
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
