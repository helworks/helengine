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
        new AssetIdentityMetadataService(TempRootPath).LoadOrCreate(sourcePath, string.Empty);
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
        AssetIdentityMetadataDocument sourceMetadata = new AssetIdentityMetadataService(TempRootPath).LoadOrCreate(sourcePath, string.Empty);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Models", "Copy.fbx");
        EditorAssetFileOperationService service = new EditorAssetFileOperationService(TempRootPath);

        service.Duplicate(sourcePath, destinationPath);

        AssetIdentityMetadataDocument destinationMetadata = new AssetIdentityMetadataService(TempRootPath).Load(destinationPath);
        Assert.True(File.Exists(destinationPath + ".hasset"));
        Assert.NotEqual(sourceMetadata.AssetId, destinationMetadata.AssetId);
        Assert.Empty(destinationMetadata.FormerAssetIds);
    }

    /// <summary>
    /// Ensures native duplication rewrites the embedded identity and never creates a sidecar.
    /// </summary>
    [Fact]
    public void Duplicate_NativeScene_MintsIndependentEmbeddedIdentityWithoutSidecar() {
        string sourcePath = Path.Combine(TempRootPath, "assets", "Source.helen");
        SceneAsset sourceAsset = new SceneAsset {
            Id = "Source.helen",
            AuthoringAssetId = Guid.NewGuid().ToString("N")
        };
        using (FileStream stream = File.Create(sourcePath)) {
            AssetSerializer.Serialize(stream, sourceAsset);
        }
        string destinationPath = Path.Combine(TempRootPath, "assets", "Copy.helen");
        EditorAssetFileOperationService service = new EditorAssetFileOperationService(TempRootPath);

        service.Duplicate(sourcePath, destinationPath);

        using FileStream destinationStream = File.OpenRead(destinationPath);
        SceneAsset destinationAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(destinationStream));
        Assert.NotEqual(sourceAsset.AuthoringAssetId, destinationAsset.AuthoringAssetId);
        Assert.Empty(destinationAsset.FormerAuthoringAssetIds);
        Assert.False(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures both current native material containers can move without destination-header inspection.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Move_NativeMaterial_PreservesEmbeddedIdentity(bool useCommonSettingsContainer) {
        string sourcePath = CreateNativeMaterial("Source.hasset", useCommonSettingsContainer);
        AssetIdentityMetadataDocument sourceIdentity = new AssetIdentityMetadataService(TempRootPath).Load(sourcePath);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Moved.hasset");

        new EditorAssetFileOperationService(TempRootPath).Move(sourcePath, destinationPath);

        AssetIdentityMetadataDocument destinationIdentity = new AssetIdentityMetadataService(TempRootPath).Load(destinationPath);
        Assert.Equal(sourceIdentity.AssetId, destinationIdentity.AssetId);
        Assert.False(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures both current native material containers duplicate with independent embedded identities.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duplicate_NativeMaterial_MintsIndependentEmbeddedIdentity(bool useCommonSettingsContainer) {
        string sourcePath = CreateNativeMaterial("Source.hasset", useCommonSettingsContainer);
        AssetIdentityMetadataDocument sourceIdentity = new AssetIdentityMetadataService(TempRootPath).Load(sourcePath);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Copy.hasset");

        new EditorAssetFileOperationService(TempRootPath).Duplicate(sourcePath, destinationPath);

        AssetIdentityMetadataDocument destinationIdentity = new AssetIdentityMetadataService(TempRootPath).Load(destinationPath);
        Assert.NotEqual(sourceIdentity.AssetId, destinationIdentity.AssetId);
        Assert.Empty(destinationIdentity.FormerAssetIds);
        Assert.False(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a destination directory link cannot redirect a move outside the assets root.
    /// </summary>
    [DirectoryLinkFact]
    public void Move_WhenDestinationDirectoryIsReparsePoint_RejectsWithoutExternalMutation() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-file-operation-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        Directory.CreateSymbolicLink(Path.Combine(TempRootPath, "outside"), outsideRoot);

        string sourcePath = CreateSource("Models/LinkedSource.fbx");
        try {
            Assert.Throws<InvalidOperationException>(() => new EditorAssetFileOperationService(TempRootPath)
                .Move(sourcePath, Path.Combine(TempRootPath, "outside", "Moved.fbx")));

            Assert.True(File.Exists(sourcePath));
            Assert.False(File.Exists(Path.Combine(outsideRoot, "Moved.fbx")));
        } finally {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    /// <summary>
    /// Ensures a case-distinct sibling is rejected on case-sensitive filesystems.
    /// </summary>
    [Fact]
    public void Duplicate_WhenCaseDistinctAssetsSiblingIsRequested_Rejects() {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        string siblingRoot = Path.Combine(TempRootPath, "ASSETS");
        Directory.CreateDirectory(siblingRoot);
        string sourcePath = CreateSource("Models/CaseSource.fbx");

        Assert.Throws<InvalidOperationException>(() => new EditorAssetFileOperationService(TempRootPath)
            .Duplicate(sourcePath, Path.Combine(TempRootPath, "ASSETS", "Copied.fbx")));

        Assert.False(File.Exists(Path.Combine(siblingRoot, "Copied.fbx")));
    }

    /// <summary>Creates one current native material fixture.</summary>
    string CreateNativeMaterial(string fileName, bool useCommonSettingsContainer) {
        string path = Path.Combine(TempRootPath, "assets", fileName);
        string authoringAssetId = Guid.NewGuid().ToString("N");
        using FileStream stream = File.Create(path);
        if (useCommonSettingsContainer) {
            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
                AuthoringAssetId = authoringAssetId
            };
            document.Importer.ImporterId = "material";
            document.Importer.AssetId = "Material";
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
        } else {
            AssetSerializer.Serialize(stream, new MaterialAsset {
                Id = "Material",
                AuthoringAssetId = authoringAssetId
            });
        }
        return path;
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
