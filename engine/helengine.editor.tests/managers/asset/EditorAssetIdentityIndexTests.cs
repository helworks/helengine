using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies stable identity indexing, lookups, and deterministic duplicate repair.
/// </summary>
public sealed class EditorAssetIdentityIndexTests : IDisposable {
    /// <summary>
    /// Temporary project root used by identity index tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated identity index project.
    /// </summary>
    public EditorAssetIdentityIndexTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-index-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));
    }

    /// <summary>
    /// Removes the isolated identity index project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures copied metadata keeps the first ordinal path as owner and rekeys the duplicate.
    /// </summary>
    [Fact]
    public void Refresh_WhenDuplicateMetadataIsCopied_RekeysNonOwnerAndKeepsFormerId() {
        string firstPath = CreateAsset("Models/A.fbx");
        string secondPath = CreateAsset("Models/B.fbx");
        CopyMetadata(firstPath, secondPath, "00112233445566778899aabbccddeeff");
        EditorAssetIdentityIndex index = CreateIndex();

        index.Initialize();

        EditorAssetIdentityEntry owner = index.FindByPath("Models/A.fbx");
        EditorAssetIdentityEntry copy = index.FindByPath("Models/B.fbx");
        Assert.Equal("00112233445566778899aabbccddeeff", owner.AssetId);
        Assert.NotEqual(owner.AssetId, copy.AssetId);
        Assert.Contains(owner.AssetId, copy.FormerAssetIds);
    }

    /// <summary>
    /// Ensures a previously recorded owner wins future duplicate repairs even when path ordering changes.
    /// </summary>
    [Fact]
    public void Refresh_WhenPreviousOwnerIsKnown_PreservesPreviousOwner() {
        string firstPath = CreateAsset("Models/Z.fbx");
        string secondPath = CreateAsset("Models/A.fbx");
        CopyMetadata(firstPath, secondPath, "00112233445566778899aabbccddeeff");
        EditorAssetIdentityIndex index = CreateIndex();

        index.Initialize();
        string firstOwnerId = index.FindByPath("Models/A.fbx").AssetId;
        AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService();
        metadataService.Save(firstPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });
        metadataService.Save(secondPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });

        index.ReconcileExternalChanges();

        Assert.Equal(firstOwnerId, index.FindByPath("Models/A.fbx").AssetId);
    }

    /// <summary>
    /// Ensures current and former UUID, path, and compatible-kind lookups return indexed entries.
    /// </summary>
    [Fact]
    public void FindMethods_ReturnCurrentFormerPathAndCompatibleEntries() {
        string assetPath = CreateAsset("Models/Only.fbx");
        AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService();
        metadataService.Save(assetPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string> { "ffeeddccbbaa99887766554433221100" }
        });
        EditorAssetIdentityIndex index = CreateIndex();
        index.Initialize();

        Assert.NotNull(index.FindByPath("Models/Only.fbx"));
        Assert.Single(index.FindByAssetId("00112233445566778899aabbccddeeff", AssetEntryKind.Model));
        Assert.Single(index.FindByAssetId("ffeeddccbbaa99887766554433221100", AssetEntryKind.Model));
        Assert.Single(index.EnumerateCompatible(AssetEntryKind.Model));
        Assert.True(index.IsCurrentAssetIdOwned("00112233445566778899aabbccddeeff"));
    }

    /// <summary>
    /// Ensures malformed existing sidecars fail visibly and are never replaced with a fresh identity.
    /// </summary>
    [Fact]
    public void Refresh_WhenExistingSidecarIsMalformed_ThrowsWithoutOverwritingMetadata() {
        string assetPath = CreateAsset("Models/Broken.fbx");
        string metadataPath = assetPath + ".hmeta";
        const string malformedJson = "{not json";
        File.WriteAllText(metadataPath, malformedJson);

        Assert.Throws<InvalidOperationException>(() => CreateIndex().Initialize());

        Assert.Equal(malformedJson, File.ReadAllText(metadataPath));
    }

    /// <summary>
    /// Ensures native animation identity is indexed from the embedded payload without creating a sidecar.
    /// </summary>
    [Fact]
    public void Refresh_ForNativeAnimationClip_IndexesEmbeddedIdentityWithoutSidecar() {
        string assetPath = CreateNativeAnimation("Animations/Indexed.hanim", "aabbccddeeff00112233445566778899");
        EditorAssetIdentityIndex index = CreateIndex();

        index.Initialize();

        EditorAssetIdentityEntry entry = index.FindByPath("Animations/Indexed.hanim");
        Assert.NotNull(entry);
        Assert.Equal("aabbccddeeff00112233445566778899", entry.AssetId);
        Assert.Equal(AssetEntryKind.File, entry.EntryKind);
        Assert.Single(index.FindByAssetId(entry.AssetId, AssetEntryKind.File));
        Assert.False(File.Exists(assetPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures deliberately duplicated native identities remain addressable and are selected deterministically by path.
    /// </summary>
    [Fact]
    public void Refresh_ForDuplicateNativeIdentities_PreservesBothEntriesForResolverSelection() {
        string firstPath = CreateNativeAnimation("Animations/A.hanim", "aabbccddeeff00112233445566778899");
        string secondPath = CreateNativeAnimation("Animations/B.hanim", "aabbccddeeff00112233445566778899");
        EditorAssetIdentityIndex index = CreateIndex();

        index.Initialize();

        IReadOnlyList<EditorAssetIdentityEntry> matches = index.FindByAssetId(
            "aabbccddeeff00112233445566778899",
            AssetEntryKind.File);
        Assert.Equal(2, matches.Count);
        Assert.Equal("Animations/A.hanim", matches[0].RelativePath);
        Assert.Equal("Animations/B.hanim", matches[1].RelativePath);
        Assert.Equal("aabbccddeeff00112233445566778899", index.FindByPath("Animations/A.hanim").AssetId);
        Assert.Equal("aabbccddeeff00112233445566778899", index.FindByPath("Animations/B.hanim").AssetId);
        Assert.False(File.Exists(firstPath + ".hmeta"));
        Assert.False(File.Exists(secondPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures repeated initialization reuses one enumerated authored-file snapshot.
    /// </summary>
    [Fact]
    public void Initialize_IsIdempotent_EnumeratesAuthoredFilesOnce() {
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        CreateAsset("Models/Initialized.fbx");
        EditorAssetIdentityIndex index = CreateIndex(catalog);

        index.Initialize();
        index.Initialize();

        Assert.Equal(1, catalog.EnumerationCount);
    }

    /// <summary>
    /// Ensures incremental registration and removal do not trigger a second full enumeration.
    /// </summary>
    [Fact]
    public void RegisterOrUpdateAndRemove_AfterInitialization_DoNotEnumerateAgain() {
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        string existingPath = CreateAsset("Models/Existing.fbx");
        EditorAssetIdentityIndex index = CreateIndex(catalog);
        index.Initialize();

        string addedPath = CreateAsset("Models/Added.fbx");
        index.RegisterOrUpdate(addedPath);
        index.Remove(existingPath);

        Assert.Equal(1, catalog.EnumerationCount);
        Assert.Null(index.FindByPath("Models/Existing.fbx"));
        Assert.NotNull(index.FindByPath("Models/Added.fbx"));
    }

    /// <summary>
    /// Ensures external reconciliation is the explicit boundary that performs another enumeration.
    /// </summary>
    [Fact]
    public void ReconcileExternalChanges_AfterInitialization_EnumeratesOnceMore() {
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        CreateAsset("Models/Initial.fbx");
        EditorAssetIdentityIndex index = CreateIndex(catalog);
        index.Initialize();

        CreateAsset("Models/External.fbx");
        index.ReconcileExternalChanges();

        Assert.Equal(2, catalog.EnumerationCount);
        Assert.NotNull(index.FindByPath("Models/External.fbx"));
    }

    /// <summary>
    /// Creates the identity index with its project-scoped dependencies.
    /// </summary>
    /// <returns>Configured identity index.</returns>
    EditorAssetIdentityIndex CreateIndex() {
        return new EditorAssetIdentityIndex(TempRootPath);
    }

    /// <summary>
    /// Creates an identity index with an instrumented authored-file catalog.
    /// </summary>
    /// <param name="catalog">Catalog used to count full authored-file enumerations.</param>
    /// <returns>Configured identity index.</returns>
    EditorAssetIdentityIndex CreateIndex(IEditorAssetFileCatalog catalog) {
        return new EditorAssetIdentityIndex(TempRootPath, null, null, null, catalog);
    }

    /// <summary>
    /// Creates one model source file below the isolated assets root.
    /// </summary>
    /// <param name="relativePath">Path relative to assets.</param>
    /// <returns>Absolute source path.</returns>
    string CreateAsset(string relativePath) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        return assetPath;
    }

    /// <summary>
    /// Writes one current native animation clip fixture with embedded authored identity.
    /// </summary>
    string CreateNativeAnimation(string relativePath, string assetId) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        using FileStream stream = File.Create(assetPath);
        AssetSerializer.Serialize(stream, new AnimationClipAsset {
            Id = relativePath,
            AuthoringAssetId = assetId,
            FormerAuthoringAssetIds = Array.Empty<string>()
        });
        return assetPath;
    }

    /// <summary>
    /// Writes one copied identity sidecar.
    /// </summary>
    /// <param name="sourcePath">Source file whose sidecar should be copied.</param>
    /// <param name="destinationPath">Destination file receiving copied metadata.</param>
    /// <param name="assetId">Duplicated stable UUID.</param>
    void CopyMetadata(string sourcePath, string destinationPath, string assetId) {
        AssetIdentityMetadataService service = new AssetIdentityMetadataService();
        service.Save(sourcePath, new AssetIdentityMetadataDocument {
            AssetId = assetId,
            FormerAssetIds = new List<string>()
        });
        File.Copy(sourcePath + ".hmeta", destinationPath + ".hmeta", true);
    }

    /// <summary>
    /// Counts authored-file enumerations while delegating enumeration to the real filesystem.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Gets the number of full authored-file enumerations requested by the index.
        /// </summary>
        public int EnumerationCount { get; private set; }

        /// <summary>
        /// Enumerates all files beneath the requested assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <returns>Filesystem paths beneath the assets root.</returns>
        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            EnumerationCount++;
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }
    }
}
