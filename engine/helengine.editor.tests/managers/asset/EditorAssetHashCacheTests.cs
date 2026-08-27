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
    public void GetContentHash_DefersPersistenceAndReusesMatchingFingerprint() {
        string assetPath = CreateAsset("Textures/Shared.png", new byte[] { 1, 2, 3 });
        CountingAssetHashCacheStore firstStore = new CountingAssetHashCacheStore();
        EditorAssetHashCache firstCache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), firstStore);

        string firstHash = firstCache.GetContentHash(assetPath);
        Assert.Equal(0, firstStore.SaveCount);
        firstCache.Flush();
        Assert.Equal(1, firstStore.SaveCount);
        firstCache.Flush();
        Assert.Equal(1, firstStore.SaveCount);

        CountingAssetHashCacheStore secondStore = new CountingAssetHashCacheStore();
        EditorAssetHashCache secondCache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), secondStore);
        string secondHash = secondCache.GetContentHash(assetPath);

        Assert.Matches("^sha256:[0-9a-f]{64}$", firstHash);
        Assert.Equal(firstHash, secondHash);
        Assert.Equal(0, secondStore.SaveCount);
    }

    /// <summary>
    /// Ensures changed source bytes invalidate a cached fingerprint.
    /// </summary>
    [Fact]
    public void GetContentHash_WhenSourceBytesChange_RecomputesHash() {
        string assetPath = CreateAsset("Textures/Changed.png", new byte[] { 1, 2, 3 });
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), new CountingAssetHashCacheStore());
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
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), new CountingAssetHashCacheStore());

        string hash = cache.GetContentHash(assetPath);
        cache.Flush();
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
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), new CountingAssetHashCacheStore());

        Assert.NotEqual(firstRawHash, secondRawHash);
        Assert.Equal(cache.GetContentHash(firstPath), cache.GetContentHash(secondPath));
    }

    /// <summary>
    /// Ensures a session hashes multiple references in memory and flushes its cache once at disposal.
    /// </summary>
    [Fact]
    public void Session_InitializesIndexOnceAndFlushesHashCacheOnceOnDispose() {
        CreateAsset("Models/A.obj", new byte[] { 1, 2, 3 });
        CreateAsset("Models/B.obj", new byte[] { 4, 5, 6 });
        CountingAssetFileCatalog catalog = new CountingAssetFileCatalog();
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore();
        CountingAssetFileHasher hasher = new CountingAssetFileHasher();
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, hasher, store);
        AssetImportManager manager = new AssetImportManager(
            TempRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(TempRootPath, "assets"))));
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(
            manager,
            cache,
            new EditorAuthoringSessionLifetime(cache),
            catalog);

        session.CreateReference("Models/A.obj", AssetEntryKind.Model);
        session.CreateReference("Models/B.obj", AssetEntryKind.Model);

        Assert.Equal(1, catalog.EnumerationCount);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(2, hasher.FileHashCount);
        session.Dispose();
        session.Dispose();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(new[] { "Models/A.obj", "Models/B.obj" }, store.LastSavedDocument.Entries.Select(entry => entry.RelativePath));
    }

    /// <summary>
    /// Ensures a dirty cache flush stores entries in ordinal path order and then becomes clean.
    /// </summary>
    [Fact]
    public void Flush_WhenEntriesAreAddedInDifferentOrder_SavesSortedEntriesOnce() {
        string firstPath = CreateAsset("Models/B.obj", new byte[] { 1, 2, 3 });
        string secondPath = CreateAsset("Models/A.obj", new byte[] { 4, 5, 6 });
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore();
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), store);

        cache.GetContentHash(firstPath);
        cache.GetContentHash(secondPath);
        cache.Flush();
        cache.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(new[] { "Models/A.obj", "Models/B.obj" }, store.LastSavedDocument.Entries.Select(entry => entry.RelativePath));
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

    /// <summary>
    /// Counts hash-cache document saves while preserving the real on-disk store behavior.
    /// </summary>
    sealed class CountingAssetHashCacheStore : IEditorAssetHashCacheStore {
        /// <summary>
        /// Gets the number of document saves requested by the cache.
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Gets the most recent document supplied to the store.
        /// </summary>
        public EditorAssetHashCacheDocument LastSavedDocument { get; private set; }

        /// <summary>
        /// Loads the current cache document from the real cache store.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <returns>Loaded cache document, or null when no valid document exists.</returns>
        public EditorAssetHashCacheDocument Load(string cachePath) {
            return new FileEditorAssetHashCacheStore().Load(cachePath);
        }

        /// <summary>
        /// Saves the cache document through the real cache store after counting the operation.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="document">Sorted cache document.</param>
        public void Save(string cachePath, EditorAssetHashCacheDocument document) {
            SaveCount++;
            LastSavedDocument = document;
            new FileEditorAssetHashCacheStore().Save(cachePath, document);
        }
    }

    /// <summary>
    /// Counts path hashing calls while using the production SHA-256 implementation.
    /// </summary>
    sealed class CountingAssetFileHasher : AssetFileHasher {
        /// <summary>
        /// Gets the number of file-path hashes requested by the cache.
        /// </summary>
        public int FileHashCount { get; private set; }

        /// <summary>
        /// Computes one file hash and records the request.
        /// </summary>
        /// <param name="filePath">Absolute file path.</param>
        /// <returns>Lowercase SHA-256 hash.</returns>
        public override string ComputeHash(string filePath) {
            FileHashCount++;
            return base.ComputeHash(filePath);
        }
    }

    /// <summary>
    /// Counts authored-file enumerations while delegating enumeration to the real filesystem.
    /// </summary>
    sealed class CountingAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Gets the number of full authored-file enumerations requested by the session index.
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
