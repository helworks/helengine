using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies ordered editor asset reference recovery and canonicalization.
/// </summary>
public sealed class EditorAssetReferenceResolverTests : IDisposable {
    /// <summary>
    /// Temporary project root used by resolver tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated resolver project.
    /// </summary>
    public EditorAssetReferenceResolverTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));
    }

    /// <summary>
    /// Removes the isolated resolver project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures a current UUID wins even when the saved path and hash are stale.
    /// </summary>
    [Fact]
    public void Resolve_CurrentAssetIdWinsOverStalePathAndHash() {
        string assetPath = CreateAsset("Models/Current.fbx", new byte[] { 1, 2, 3 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        metadata.Save(assetPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Missing.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(assetPath, result.FullPath);
        Assert.Equal("Models/Current.fbx", result.CanonicalReference.RelativePath);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.True(result.ReferenceChanged);
    }

    /// <summary>
    /// Ensures a missing sidecar adopts an unclaimed saved UUID during path recovery.
    /// </summary>
    [Fact]
    public void Resolve_ExistingPathWithoutMetadata_AdoptsUnclaimedSavedAssetId() {
        string assetPath = CreateAsset("Models/Adopt.fbx", new byte[] { 4, 5, 6 });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Adopt.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.Path, result.Tier);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.True(result.MetadataChanged);
        Assert.True(File.Exists(assetPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a missing sidecar at the saved path cannot override a UUID already owned by another asset.
    /// </summary>
    [Fact]
    public void Resolve_WhenSavedPathMetadataIsMissing_ExistingAssetIdOwnerStillWins() {
        string idOwnerPath = CreateAsset("Models/A.fbx", new byte[] { 1, 2, 3 });
        string savedPath = CreateAsset("Models/B.fbx", new byte[] { 4, 5, 6 });
        AssetIdentityMetadataService metadata = new AssetIdentityMetadataService();
        metadata.Save(idOwnerPath, new AssetIdentityMetadataDocument {
            AssetId = "00112233445566778899aabbccddeeff",
            FormerAssetIds = new List<string>()
        });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/B.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(idOwnerPath, result.FullPath);
        Assert.NotEqual(savedPath, result.FullPath);
    }

    /// <summary>
    /// Ensures hash fallback selects the ordinally smallest compatible candidate.
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyHashMatches_SelectsOrdinalCompatiblePath() {
        string firstPath = CreateAsset("Models/A.fbx", new byte[] { 9, 9, 9 });
        string secondPath = CreateAsset("Models/B.fbx", new byte[] { 9, 9, 9 });
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference firstReference = setupResolver.CreateFileReference(firstPath, AssetEntryKind.Model);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference unresolvedReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "ffeeddccbbaa99887766554433221100",
            "Models/Missing.fbx",
            firstReference.ContentHash);

        AssetReferenceResolution result = resolver.Resolve(unresolvedReference, AssetEntryKind.Model);

        Assert.Equal(AssetReferenceResolutionTier.ContentHash, result.Tier);
        Assert.Equal("Models/A.fbx", result.CanonicalReference.RelativePath);
        Assert.NotEqual(firstPath, secondPath);
    }

    /// <summary>
    /// Ensures unresolved diagnostics contain all supplied identity fields and attempted tiers.
    /// </summary>
    [Fact]
    public void Resolve_WhenNoCandidateExists_ThrowsCompleteDiagnostic() {
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "00112233445566778899aabbccddeeff",
            "Models/Missing.fbx",
            "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.Resolve(reference, AssetEntryKind.Model));

        Assert.Contains("Model", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.AssetId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.RelativePath, exception.Message, StringComparison.Ordinal);
        Assert.Contains(reference.ContentHash, exception.Message, StringComparison.Ordinal);
        Assert.Contains("AssetId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Path", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ContentHash", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a native material reference recovers after its file moves by the embedded authored identity.
    /// </summary>
    [Fact]
    public void Resolve_NativeHelmatRecoversMovedMaterialByEmbeddedAssetId() {
        string sourcePath = CreateNativeHelmat("Materials/Source.helmat", "00112233445566778899aabbccddeeff");
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = setupResolver.CreateFileReference(sourcePath, AssetEntryKind.Material);
        string destinationPath = Path.Combine(TempRootPath, "assets", "Materials", "Moved.helmat");
        File.Move(sourcePath, destinationPath);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Material);

        Assert.Equal(AssetReferenceResolutionTier.AssetId, result.Tier);
        Assert.Equal(destinationPath, result.FullPath);
        Assert.Equal("00112233445566778899aabbccddeeff", result.CanonicalReference.AssetId);
        Assert.Equal("Materials/Moved.helmat", result.CanonicalReference.RelativePath);
        Assert.False(File.Exists(destinationPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures a replacement native material with a different identity is recovered by its identity-excluded content hash.
    /// </summary>
    [Fact]
    public void Resolve_NativeHelmatReplacementRecoversByContentHash() {
        string sourcePath = CreateNativeHelmat("Materials/Source.helmat", "3344556677889900aabbccddeeff1122");
        EditorAssetReferenceResolver setupResolver = new EditorAssetReferenceResolver(TempRootPath);
        SceneAssetReference reference = setupResolver.CreateFileReference(sourcePath, AssetEntryKind.Material);
        File.Delete(sourcePath);
        string replacementPath = CreateNativeHelmat("Materials/Replacement.helmat", "44556677889900aabbccddeeff112233");
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        AssetReferenceResolution result = resolver.Resolve(reference, AssetEntryKind.Material);

        Assert.Equal(AssetReferenceResolutionTier.ContentHash, result.Tier);
        Assert.Equal(replacementPath, result.FullPath);
        Assert.Equal("44556677889900aabbccddeeff112233", result.CanonicalReference.AssetId);
        Assert.Equal("Materials/Replacement.helmat", result.CanonicalReference.RelativePath);
        Assert.False(File.Exists(replacementPath + ".hmeta"));
    }

    /// <summary>
    /// Ensures resolver operations reuse one initialized index without implicit full rescans.
    /// </summary>
    [Fact]
    public void Resolve_MultipleReferences_ReusesInitializedIndexWithoutRescanning() {
        string firstPath = CreateAsset("Models/A.fbx", new byte[] { 1, 2, 3 });
        string secondPath = CreateAsset("Models/B.fbx", new byte[] { 4, 5, 6 });
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        EditorAssetIdentityIndex index = new EditorAssetIdentityIndex(TempRootPath, null, null, null, catalog);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, index);

        resolver.CreateFileReference(firstPath, AssetEntryKind.Model);
        resolver.CreateFileReference(secondPath, AssetEntryKind.Model);

        Assert.Equal(1, catalog.EnumerationCount);
    }

    /// <summary>
    /// Ensures a resolver flushes a cache it creates and repeated disposal is harmless.
    /// </summary>
    [Fact]
    public void Dispose_WhenResolverOwnsHashCache_FlushesExactlyOnce() {
        string assetPath = CreateAsset("Models/Owned.fbx", new byte[] { 7, 8, 9 });
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath);

        resolver.CreateFileReference(assetPath, AssetEntryKind.Model);
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        Assert.False(File.Exists(cachePath));

        resolver.Dispose();
        resolver.Dispose();

        Assert.True(File.Exists(cachePath));
        string persisted = File.ReadAllText(cachePath);
        resolver = null;
        Assert.Contains("Models/Owned.fbx", persisted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a resolver borrowing a caller cache does not flush or release it.
    /// </summary>
    [Fact]
    public void Dispose_WhenResolverBorrowsHashCache_LeavesCacheLifetimeWithCaller() {
        string assetPath = CreateAsset("Models/Borrowed.fbx", new byte[] { 2, 4, 6 });
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);
        EditorAssetReferenceResolver resolver = new EditorAssetReferenceResolver(TempRootPath, hashCache: cache);

        resolver.CreateFileReference(assetPath, AssetEntryKind.Model);
        resolver.Dispose();

        Assert.False(File.Exists(cachePath));
        cache.Dispose();
        Assert.True(File.Exists(cachePath));
        cache.Dispose();
    }

    /// <summary>
    /// Creates one source file below the isolated assets root.
    /// </summary>
    /// <param name="relativePath">Path relative to assets.</param>
    /// <param name="bytes">Source bytes.</param>
    /// <returns>Absolute source path.</returns>
    string CreateAsset(string relativePath, byte[] bytes) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        File.WriteAllBytes(assetPath, bytes);
        return assetPath;
    }

    /// <summary>
    /// Writes one current native material common-settings document with embedded authored identity.
    /// </summary>
    string CreateNativeHelmat(string relativePath, string assetId) {
        string assetPath = Path.Combine(TempRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument {
            AuthoringAssetId = assetId
        };
        document.Importer.ImporterId = "helengine.material";
        document.Importer.AssetId = "Materials/Native";
        using FileStream stream = File.Create(assetPath);
        MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
        return assetPath;
    }

    /// <summary>
    /// Counts authored-file enumerations while delegating enumeration to the real filesystem.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Gets the number of full authored-file enumerations requested by the resolver index.
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
