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
}
