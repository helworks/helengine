namespace helengine.editor.tests;

/// <summary>
/// Verifies the packaged runtime asset-loading seam no longer depends on editor-side serializer sources.
/// </summary>
public sealed class RuntimeAssetSerializerSourceTests {
    /// <summary>
    /// Ensures the packaged runtime asset serializer entry point no longer references the editor asset serializer.
    /// </summary>
    [Fact]
    public void AssetSerializer_source_uses_packaged_asset_binary_serializer() {
        string sourcePath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "assets",
            "AssetSerializer.cs");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("PackagedAssetBinarySerializer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EditorAssetBinarySerializer", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the editor asset serializer implementation is no longer compiled into the packaged runtime assembly.
    /// </summary>
    [Fact]
    public void HelengineCore_source_does_not_contain_editor_asset_binary_serializer() {
        string serializerPath = Path.Combine(
            ResolveRepositoryRootPath(),
            "engine",
            "helengine.core",
            "assets",
            "EditorAssetBinarySerializer.cs");

        Assert.False(File.Exists(serializerPath), "helengine.core must not ship the editor asset serializer.");
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
