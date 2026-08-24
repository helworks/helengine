using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies the asset browser uses shared authored-file classification and hides identity sidecars.
/// </summary>
public sealed class EditorAssetManagerTests : IDisposable {
    /// <summary>
    /// Temporary project root used by asset browser tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated asset browser project.
    /// </summary>
    public EditorAssetManagerTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));
    }

    /// <summary>
    /// Removes the isolated asset browser project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures identity metadata sidecars never appear as browser entries while authored files remain visible.
    /// </summary>
    [Fact]
    public void LoadEntries_HidesIdentityMetadataSidecars() {
        string assetPath = Path.Combine(TempRootPath, "assets", "Visible.png");
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        File.WriteAllText(assetPath + ".hmeta", "{}");
        EditorAssetManager manager = new EditorAssetManager(TempRootPath);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

        manager.LoadEntries(entries);

        Assert.Contains(entries, entry => entry.Name == "Visible.png");
        Assert.DoesNotContain(entries, entry => entry.Name == "Visible.png.hmeta");
    }

    /// <summary>
    /// Ensures the shared classifier distinguishes metadata sidecars from authored material sidecars.
    /// </summary>
    [Fact]
    public void EditorAssetPathClassifier_HidesMetadataAndUnrecognizedImporterSidecars() {
        EditorAssetPathClassifier classifier = new EditorAssetPathClassifier();
        string metadataPath = Path.Combine(TempRootPath, "assets", "Texture.png.hmeta");
        string importerPath = Path.Combine(TempRootPath, "assets", "Texture.png.hasset");
        string materialPath = Path.Combine(TempRootPath, "assets", "Material.hasset");
        using (FileStream stream = File.Create(materialPath)) {
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                global::helengine.files.EditorAssetBinarySerializer.CurrentVersion,
                global::helengine.files.EditorAssetBinarySerializer.FormatId,
                (ushort)EditorBinaryRecordKind.Asset,
                (ushort)EditorAssetBinaryValueKind.MaterialAsset));
        }

        Assert.True(classifier.ShouldHide(metadataPath));
        Assert.True(classifier.ShouldHide(importerPath));
        Assert.False(classifier.ShouldHide(materialPath));
    }
}
