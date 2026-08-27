using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies strict persistence and validation of authored asset identity sidecars.
/// </summary>
public sealed class AssetIdentityMetadataServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the metadata tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated metadata test project.
    /// </summary>
    public AssetIdentityMetadataServiceTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));
    }

    /// <summary>
    /// Removes the isolated metadata test project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures first use creates the exact versioned sidecar contract.
    /// </summary>
    [Fact]
    public void LoadOrCreate_CreatesVersionedJsonSidecar() {
        string assetPath = CreateAsset("Textures/Shared.png");
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        AssetIdentityMetadataDocument document = service.LoadOrCreate(assetPath, string.Empty);
        string json = File.ReadAllText(assetPath + ".hmeta");

        Assert.Matches("^[0-9a-f]{32}$", document.AssetId);
        Assert.Empty(document.FormerAssetIds);
        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"assetId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"formerAssetIds\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a caller-provided valid UUID is preserved when creating metadata.
    /// </summary>
    [Fact]
    public void LoadOrCreate_WithRequestedAssetId_PreservesRequestedId() {
        string assetPath = CreateAsset("Models/Shared.gltf");
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        AssetIdentityMetadataDocument document = service.LoadOrCreate(assetPath, "00112233445566778899aabbccddeeff");

        Assert.Equal("00112233445566778899aabbccddeeff", document.AssetId);
    }

    /// <summary>
    /// Ensures malformed current and former ids are rejected with the metadata path in the failure.
    /// </summary>
    [Fact]
    public void Load_RejectsMalformedIdsAndIncludesMetadataPath() {
        string assetPath = CreateAsset("Textures/Malformed.png");
        string metadataPath = assetPath + ".hmeta";
        File.WriteAllText(metadataPath, "{\"version\":1,\"assetId\":\"bad\",\"formerAssetIds\":[]}");
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Load(assetPath));

        Assert.Contains(metadataPath, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures unsupported metadata versions and duplicate former ids are rejected.
    /// </summary>
    [Fact]
    public void Load_RejectsUnsupportedVersionAndDuplicateFormerIds() {
        string assetPath = CreateAsset("Textures/Invalid.png");
        string metadataPath = assetPath + ".hmeta";
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        File.WriteAllText(metadataPath, "{\"version\":2,\"assetId\":\"00112233445566778899aabbccddeeff\",\"formerAssetIds\":[]}");
        Assert.Throws<InvalidOperationException>(() => service.Load(assetPath));

        File.WriteAllText(metadataPath, "{\"version\":1,\"assetId\":\"00112233445566778899aabbccddeeff\",\"formerAssetIds\":[\"ffeeddccbbaa99887766554433221100\",\"ffeeddccbbaa99887766554433221100\"]}");
        Assert.Throws<InvalidOperationException>(() => service.Load(assetPath));
    }

    /// <summary>
    /// Ensures metadata cannot be created for a missing authored source file.
    /// </summary>
    [Fact]
    public void LoadOrCreate_WhenSourceIsMissing_Throws() {
        string assetPath = Path.Combine(TempRootPath, "assets", "Missing.png");
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.LoadOrCreate(assetPath, string.Empty));

        Assert.Contains(assetPath, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures engine-native authored files carry their stable identity inside the binary payload.
    /// A regression that routes native files through sidecars makes the sidecar assertions fail.
    /// </summary>
    [Fact]
    public void Save_ForNativeAuthoredFormats_EmbedsIdentityWithoutCreatingSidecars() {
        string scenePath = Path.Combine(TempRootPath, "assets", "Native.helen");
        string blueprintPath = Path.Combine(TempRootPath, "assets", "Native.hblueprint");
        string materialPath = Path.Combine(TempRootPath, "assets", "Native.hasset");
        WriteNativeScene(scenePath);
        WriteNativeBlueprint(blueprintPath);
        WriteNativeMaterial(materialPath);
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();
        AssetIdentityMetadataDocument expected = new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string> { "ffeeddccbbaa99887766554433221100" }
        };

        service.Save(scenePath, expected);
        service.Save(blueprintPath, expected);
        service.Save(materialPath, expected);

        foreach (string path in new[] { scenePath, blueprintPath, materialPath }) {
            AssetIdentityMetadataDocument loaded = service.Load(path);
            Assert.Equal(expected.AssetId, loaded.AssetId);
            Assert.Equal(expected.FormerAssetIds, loaded.FormerAssetIds);
            Assert.False(File.Exists(path + ".hmeta"));
        }
    }

    /// <summary>
    /// Ensures importer-generated material settings use their embedded identity without creating a nested sidecar.
    /// </summary>
    [Fact]
    public void Load_ForImportedMaterialSettings_UsesEmbeddedIdentityWithoutSidecar() {
        string materialPath = Path.Combine(TempRootPath, "assets", "models", "Model", "Material.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);
        MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
            AuthoringAssetId = "00112233445566778899aabbccddeeff"
        };
        document.Importer.ImporterId = "helengine.material";
        document.Importer.AssetId = "models/Material.hasset";
        using (FileStream stream = File.Create(materialPath)) {
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
        }

        AssetIdentityMetadataDocument loaded = new AssetIdentityMetadataService().Load(materialPath);

        Assert.Equal(document.AuthoringAssetId, loaded.AssetId);
        Assert.False(File.Exists(materialPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a native file without the current embedded identity is rejected.
    /// </summary>
    [Fact]
    public void LoadOrCreate_WhenNativeIdentityIsMissing_RejectsFile() {
        string scenePath = Path.Combine(TempRootPath, "assets", "MissingIdentity.helen");
        WriteNativeScene(scenePath);
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.LoadOrCreate(scenePath, string.Empty));

        Assert.Contains("embedded", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(scenePath + ".hmeta"));
    }

    /// <summary>
    /// Writes one minimal native scene fixture without authored identity metadata.
    /// </summary>
    static void WriteNativeScene(string path) {
        using FileStream stream = File.Create(path);
        AssetSerializer.Serialize(stream, new SceneAsset {
            Id = "Native",
            RootEntities = Array.Empty<SceneEntityAsset>(),
            AssetReferences = Array.Empty<SceneAssetReference>()
        });
    }

    /// <summary>
    /// Writes one minimal native blueprint fixture without authored identity metadata.
    /// </summary>
    static void WriteNativeBlueprint(string path) {
        using FileStream stream = File.Create(path);
        AssetSerializer.Serialize(stream, new BlueprintAsset {
            Id = "Native",
            RootEntity = new SceneEntityAsset {
                Id = 1u,
                Name = "Root",
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            },
            AssetReferences = Array.Empty<SceneAssetReference>()
        });
    }

    /// <summary>
    /// Writes one minimal native material-settings fixture without authored identity metadata.
    /// </summary>
    static void WriteNativeMaterial(string path) {
        MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument();
        document.Importer.ImporterId = "native-material";
        document.Importer.AssetId = "Native";
        using FileStream stream = File.Create(path);
        MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
    }

    /// <summary>
    /// Creates one source asset below the isolated assets root.
    /// </summary>
    /// <param name="relativePath">Source path relative to the assets root.</param>
    /// <returns>Absolute source path.</returns>
    string CreateAsset(string relativePath) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        return assetPath;
    }
}
