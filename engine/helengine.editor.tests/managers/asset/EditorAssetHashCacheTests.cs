using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies fingerprinted editor asset hash caching.
/// </summary>
public sealed class EditorAssetHashCacheTests : IDisposable {
    /// <summary>
    /// Temporary project root used by cache tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes one isolated cache project.
    /// </summary>
    public EditorAssetHashCacheTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-hash-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempRootPath, "assets"));
    }

    /// <summary>
    /// Removes the isolated cache project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures hashes are prefixed, persisted, and reused for a matching file fingerprint.
    /// </summary>
    [Fact]
    public void GetContentHash_PersistsAndReusesMatchingFingerprint() {
        string assetPath = CreateAsset("Textures/Shared.png", new byte[] { 1, 2, 3 });
        EditorAssetHashCache firstCache = new EditorAssetHashCache(TempRootPath);

        string firstHash = firstCache.GetContentHash(assetPath);
        EditorAssetHashCache secondCache = new EditorAssetHashCache(TempRootPath);
        string secondHash = secondCache.GetContentHash(assetPath);

        Assert.Matches("^sha256:[0-9a-f]{64}$", firstHash);
        Assert.Equal(firstHash, secondHash);
        Assert.True(File.Exists(Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json")));
    }

    /// <summary>
    /// Ensures changed source bytes invalidate a cached fingerprint.
    /// </summary>
    [Fact]
    public void GetContentHash_WhenSourceBytesChange_RecomputesHash() {
        string assetPath = CreateAsset("Textures/Changed.png", new byte[] { 1, 2, 3 });
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);
        string firstHash = cache.GetContentHash(assetPath);

        File.WriteAllBytes(assetPath, new byte[] { 4, 5, 6, 7 });
        string secondHash = cache.GetContentHash(assetPath);

        Assert.NotEqual(firstHash, secondHash);
    }

    /// <summary>
    /// Ensures malformed disposable cache JSON is ignored and rebuilt from the source file.
    /// </summary>
    [Fact]
    public void GetContentHash_WhenCacheJsonIsMalformed_RebuildsCache() {
        string assetPath = CreateAsset("Textures/Rebuild.png", new byte[] { 9, 8, 7 });
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
        File.WriteAllText(cachePath, "not-json");
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);

        string hash = cache.GetContentHash(assetPath);
        string rebuiltJson = File.ReadAllText(cachePath);

        Assert.Matches("^sha256:[0-9a-f]{64}$", hash);
        Assert.Contains("rebuild.png", rebuiltJson, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures embedded native identities do not affect the semantic recovery hash.
    /// A regression that hashes the complete native file makes the cache hashes differ.
    /// </summary>
    [Fact]
    public void GetContentHash_ForNativeFiles_ExcludesEmbeddedIdentityMetadata() {
        string firstPath = Path.Combine(TempRootPath, "assets", "First.helen");
        string secondPath = Path.Combine(TempRootPath, "assets", "Second.helen");
        WriteNativeScene(firstPath);
        WriteNativeScene(secondPath);
        AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService();
        metadataService.Save(firstPath, new AssetIdentityMetadataDocument { AssetId = "00112233445566778899aabbccddeeff" });
        metadataService.Save(secondPath, new AssetIdentityMetadataDocument { AssetId = "ffeeddccbbaa99887766554433221100" });

        string firstRawHash = new AssetFileHasher().ComputeHash(firstPath);
        string secondRawHash = new AssetFileHasher().ComputeHash(secondPath);
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);

        Assert.NotEqual(firstRawHash, secondRawHash);
        Assert.Equal(cache.GetContentHash(firstPath), cache.GetContentHash(secondPath));
    }

    /// <summary>
    /// Writes a deterministic native scene fixture used to compare semantic hashes.
    /// </summary>
    static void WriteNativeScene(string path) {
        using FileStream stream = File.Create(path);
        AssetSerializer.Serialize(stream, new SceneAsset {
            Id = "Shared",
            RootEntities = Array.Empty<SceneEntityAsset>(),
            AssetReferences = Array.Empty<SceneAssetReference>()
        });
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
