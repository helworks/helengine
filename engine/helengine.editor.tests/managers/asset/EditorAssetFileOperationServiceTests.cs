using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies editor asset move and duplicate operations preserve identity sidecar semantics.
/// </summary>
public sealed class EditorAssetFileOperationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by file-operation tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated file-operation project.
    /// </summary>
    public EditorAssetFileOperationServiceTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-file-operation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));
    }

    /// <summary>
    /// Removes the isolated file-operation project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures move carries source, importer sidecar, and identity sidecar together.
    /// </summary>
    [Fact]
    public void Move_CarriesSourceImporterAndIdentitySidecars() {
        string sourcePath = CreateSource("Models/Source.fbx");
        File.WriteAllText(sourcePath + ".hasset", "importer");
        new AssetIdentityMetadataService().LoadOrCreate(sourcePath, string.Empty);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Models", "Moved.fbx");
        EditorAssetFileOperationService service = new EditorAssetFileOperationService(TempRootPath);

        service.Move(sourcePath, destinationPath);

        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(destinationPath));
        Assert.True(File.Exists(destinationPath + ".hasset"));
        Assert.True(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures duplicate copies receive independent metadata and do not inherit former UUID aliases.
    /// </summary>
    [Fact]
    public void Duplicate_CopiesImporterButMintsIndependentIdentity() {
        string sourcePath = CreateSource("Models/Source.fbx");
        File.WriteAllText(sourcePath + ".hasset", "importer");
        AssetIdentityMetadataDocument sourceMetadata = new AssetIdentityMetadataService().LoadOrCreate(sourcePath, string.Empty);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Models", "Copy.fbx");
        EditorAssetFileOperationService service = new EditorAssetFileOperationService(TempRootPath);

        service.Duplicate(sourcePath, destinationPath);

        AssetIdentityMetadataDocument destinationMetadata = new AssetIdentityMetadataService().Load(destinationPath);
        Assert.True(File.Exists(destinationPath + ".hasset"));
        Assert.NotEqual(sourceMetadata.AssetId, destinationMetadata.AssetId);
        Assert.Empty(destinationMetadata.FormerAssetIds);
    }

    /// <summary>
    /// Creates one source asset below the isolated assets root.
    /// </summary>
    /// <param name="relativePath">Path relative to assets.</param>
    /// <returns>Absolute source path.</returns>
    string CreateSource(string relativePath) {
        string sourcePath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });
        return sourcePath;
    }
}
