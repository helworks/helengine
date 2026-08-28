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
        CountingAssetHashCacheStore firstStore = new CountingAssetHashCacheStore(TempRootPath);
        EditorAssetHashCache firstCache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), firstStore);

        string firstHash = firstCache.GetContentHash(assetPath);
        Assert.Equal(0, firstStore.SaveCount);
        firstCache.Flush();
        Assert.Equal(1, firstStore.SaveCount);
        firstCache.Flush();
        Assert.Equal(1, firstStore.SaveCount);

        CountingAssetHashCacheStore secondStore = new CountingAssetHashCacheStore(TempRootPath);
        CountingAssetFileHasher secondHasher = new CountingAssetFileHasher(TempRootPath);
        EditorAssetHashCache secondCache = new EditorAssetHashCache(TempRootPath, secondHasher, secondStore);
        string secondHash = secondCache.GetContentHash(assetPath);

        Assert.Matches("^sha256:[0-9a-f]{64}$", firstHash);
        Assert.Equal(firstHash, secondHash);
        Assert.Equal(0, secondStore.SaveCount);
        Assert.Equal(0, secondHasher.FileHashCount);
    }

    /// <summary>
    /// Ensures sequential cache flushes merge dirty paths instead of dropping another cache's update.
    /// </summary>
    [Fact]
    public void Flush_WhenTwoCachesHashDifferentFiles_PreservesBothDirtyUpdates() {
        string seededPath = CreateAsset("Models/Seed.obj", new byte[] { 0, 1, 2 });
        string firstPath = CreateAsset("Models/First.obj", new byte[] { 3, 4, 5 });
        string secondPath = CreateAsset("Models/Second.obj", new byte[] { 6, 7, 8 });

        using (EditorAssetHashCache seedCache = new EditorAssetHashCache(TempRootPath)) {
            seedCache.GetContentHash(seededPath);
        }

        string firstHash;
        string secondHash;
        using (EditorAssetHashCache firstCache = new EditorAssetHashCache(TempRootPath))
        using (EditorAssetHashCache secondCache = new EditorAssetHashCache(TempRootPath)) {
            firstHash = firstCache.GetContentHash(firstPath);
            secondHash = secondCache.GetContentHash(secondPath);
            firstCache.Flush();
            secondCache.Flush();
        }

        CountingAssetFileHasher finalHasher = new CountingAssetFileHasher(TempRootPath);
        using EditorAssetHashCache finalCache = new EditorAssetHashCache(TempRootPath, finalHasher);
        Assert.Equal(firstHash, finalCache.GetContentHash(firstPath));
        Assert.Equal(secondHash, finalCache.GetContentHash(secondPath));
        Assert.Equal(0, finalHasher.FileHashCount);
    }

    /// <summary>
    /// Ensures overlapping flushes merge at the store boundary rather than overwriting each other's updates.
    /// </summary>
    [Fact]
    public async Task Flush_WhenTwoCachesOverlapAtStoreBoundary_PreservesBothDirtyUpdates() {
        string firstPath = CreateAsset("Models/ConcurrentFirst.obj", new byte[] { 3, 4, 5 });
        string secondPath = CreateAsset("Models/ConcurrentSecond.obj", new byte[] { 6, 7, 8 });
        OverlappingAssetHashCacheStore store = new OverlappingAssetHashCacheStore(TempRootPath);
        using EditorAssetHashCache firstCache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);
        using EditorAssetHashCache secondCache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);

        firstCache.GetContentHash(firstPath);
        secondCache.GetContentHash(secondPath);
        Task firstFlush = Task.Run(firstCache.Flush);
        Task secondFlush = Task.Run(secondCache.Flush);
        await Task.WhenAll(firstFlush, secondFlush);

        CountingAssetFileHasher finalHasher = new CountingAssetFileHasher(TempRootPath);
        using EditorAssetHashCache finalCache = new EditorAssetHashCache(TempRootPath, finalHasher);
        Assert.Equal("sha256:" + new AssetFileHasher(TempRootPath).ComputeHash(firstPath), finalCache.GetContentHash(firstPath));
        Assert.Equal("sha256:" + new AssetFileHasher(TempRootPath).ComputeHash(secondPath), finalCache.GetContentHash(secondPath));
        Assert.Equal(0, finalHasher.FileHashCount);
    }

    /// <summary>
    /// Ensures a disposed cache rejects hashing and flushing without creating a dirty cache document.
    /// </summary>
    [Fact]
    public void Dispose_WhenRepeated_RejectsHashAndFlushAfterRelease() {
        string assetPath = CreateAsset("Models/Disposed.obj", new byte[] { 1, 2, 3 });
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath);

        cache.Dispose();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetContentHash(assetPath));
        Assert.Throws<ObjectDisposedException>(() => cache.Flush());
        Assert.False(File.Exists(cachePath));
    }

    /// <summary>
    /// Ensures changed source bytes invalidate a cached fingerprint.
    /// </summary>
    [Fact]
    public void GetContentHash_WhenSourceBytesChange_RecomputesHash() {
        string assetPath = CreateAsset("Textures/Changed.png", new byte[] { 1, 2, 3 });
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), new CountingAssetHashCacheStore(TempRootPath));
        string firstHash = cache.GetContentHash(assetPath);

        File.WriteAllBytes(assetPath, new byte[] { 4, 5, 6, 7 });
        string secondHash = cache.GetContentHash(assetPath);

        Assert.NotEqual(firstHash, secondHash);
    }

    /// <summary>
    /// Ensures explicit invalidation defeats a stale same-length, same-timestamp fingerprint.
    /// </summary>
    [Fact]
    public void InvalidateContentHash_WhenBytesChangeButFingerprintDoesNot_RecomputesHash() {
        string assetPath = CreateAsset("Textures/SameFingerprint.png", new byte[] { 1, 2, 3, 4 });
        CountingAssetFileHasher hasher = new CountingAssetFileHasher(TempRootPath);
        using EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, hasher, new CountingAssetHashCacheStore(TempRootPath));

        string firstHash = cache.GetContentHash(assetPath);
        DateTime originalTimestamp = File.GetLastWriteTimeUtc(assetPath);
        File.WriteAllBytes(assetPath, new byte[] { 5, 6, 7, 8 });
        File.SetLastWriteTimeUtc(assetPath, originalTimestamp);

        cache.InvalidateContentHash(assetPath);
        string secondHash = cache.GetContentHash(assetPath);

        Assert.NotEqual(firstHash, secondHash);
        Assert.Equal(2, hasher.FileHashCount);
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
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), new CountingAssetHashCacheStore(TempRootPath));

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
        AssetIdentityMetadataService metadataService = new AssetIdentityMetadataService(TempRootPath);
        metadataService.Save(firstPath, new AssetIdentityMetadataDocument { AssetId = "00112233445566778899aabbccddeeff" });
        metadataService.Save(secondPath, new AssetIdentityMetadataDocument { AssetId = "ffeeddccbbaa99887766554433221100" });

        string firstRawHash = new AssetFileHasher(TempRootPath).ComputeHash(firstPath);
        string secondRawHash = new AssetFileHasher(TempRootPath).ComputeHash(secondPath);
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), new CountingAssetHashCacheStore(TempRootPath));

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
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore(TempRootPath);
        CountingAssetFileHasher hasher = new CountingAssetFileHasher(TempRootPath);
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, hasher, store);
        EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(TempRootPath, null, null, cache, catalog);
        identityIndex.Initialize();
        EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(TempRootPath, identityIndex, cache);
        EditorNativeAssetWriteService nativeAssetWriteService = new EditorNativeAssetWriteService(TempRootPath, identityIndex, cache);
        EditorProjectAuthoringSessionResources resources = new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, cache, nativeAssetWriteService);
        AssetImportManager manager = new AssetImportManager(
            TempRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(TempRootPath, "assets"))));
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(
            manager,
            cache,
            identityIndex,
            referenceResolver,
            new EditorAuthoringSessionLifetime(resources),
            nativeAssetWriteService);

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
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore(TempRootPath);
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);

        cache.GetContentHash(firstPath);
        cache.GetContentHash(secondPath);
        cache.Flush();
        cache.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(new[] { "Models/A.obj", "Models/B.obj" }, store.LastSavedDocument.Entries.Select(entry => entry.RelativePath));
    }

    /// <summary>
    /// Ensures a failed store update leaves dirty state available for a later successful flush.
    /// </summary>
    [Fact]
    public void Flush_WhenStoreUpdateFails_RetainsDirtyState() {
        string assetPath = CreateAsset("Models/Retry.obj", new byte[] { 1, 2, 3 });
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore(TempRootPath) { FailNextUpdate = true };
        using EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);
        cache.GetContentHash(assetPath);

        Assert.Throws<IOException>(() => cache.Flush());
        Assert.Equal(0, store.SaveCount);
        cache.Flush();

        Assert.Equal(1, store.SaveCount);
        Assert.Contains(store.LastSavedDocument.Entries, entry => entry.RelativePath == "Models/Retry.obj");
    }

    /// <summary>
    /// Ensures a rehashed path still carries its deletion tombstone when persistence fails.
    /// </summary>
    [Fact]
    public void Flush_WhenRehashUpdateFails_RetainsDeletionTombstone() {
        string assetPath = CreateAsset("Models/TombstoneRetry.obj", new byte[] { 1, 2, 3 });
        using (EditorAssetHashCache seedCache = new EditorAssetHashCache(TempRootPath)) {
            seedCache.GetContentHash(assetPath);
        }

        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore(TempRootPath) { FailNextUpdate = true };
        using EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);
        cache.GetContentHash(assetPath);
        File.WriteAllBytes(assetPath, new byte[] { 4, 5, 6 });
        cache.InvalidateContentHash(assetPath);
        cache.GetContentHash(assetPath);

        Assert.Throws<IOException>(() => cache.Flush());
        Assert.Contains("Models/TombstoneRetry.obj", store.LastRemovedPaths);
    }

    /// <summary>
    /// Ensures session disposal remains retryable when the owned cache store fails once.
    /// </summary>
    [Fact]
    public void SessionDispose_WhenCacheFlushFailsOnce_RetriesOnSecondDispose() {
        CreateAsset("Models/SessionRetry.obj", new byte[] { 1, 2, 3 });
        CountingAssetHashCacheStore store = new CountingAssetHashCacheStore(TempRootPath) { FailNextUpdate = true };
        EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(TempRootPath), store);
        EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(TempRootPath, null, null, cache);
        identityIndex.Initialize();
        EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(TempRootPath, identityIndex, cache);
        EditorNativeAssetWriteService nativeAssetWriteService = new EditorNativeAssetWriteService(TempRootPath, identityIndex, cache);
        EditorProjectAuthoringSessionResources resources = new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, cache, nativeAssetWriteService);
        AssetImportManager manager = new AssetImportManager(
            TempRootPath,
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(TempRootPath, "assets"))));
        EditorProjectAuthoringSession session = new EditorProjectAuthoringSession(
            manager,
            cache,
            identityIndex,
            referenceResolver,
            new EditorAuthoringSessionLifetime(resources),
            nativeAssetWriteService);

        session.CreateReference("Models/SessionRetry.obj", AssetEntryKind.Model);

        Assert.Throws<IOException>(() => session.Dispose());
        session.Dispose();

        Assert.Equal(1, store.SaveCount);
        Assert.NotNull(store.LastSavedDocument);
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
        readonly FileEditorAssetHashCacheStore InnerStore;

        public CountingAssetHashCacheStore(string projectRootPath) {
            InnerStore = new FileEditorAssetHashCacheStore(projectRootPath);
        }

        /// <summary>
        /// Gets the number of document saves requested by the cache.
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Gets or sets whether the next update should fail before writing.
        /// </summary>
        public bool FailNextUpdate { get; set; }

        /// <summary>
        /// Gets the most recent document supplied to the store.
        /// </summary>
        public EditorAssetHashCacheDocument LastSavedDocument { get; private set; }

        /// <summary>
        /// Gets the most recent deletion tombstones supplied to the store.
        /// </summary>
        public IReadOnlyCollection<string> LastRemovedPaths { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Loads the current cache document from the real cache store.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <returns>Loaded cache document, or null when no valid document exists.</returns>
        public EditorAssetHashCacheDocument Load(string cachePath) {
            return InnerStore.Load(cachePath);
        }

        /// <summary>
        /// Saves the cache document through the real cache store after counting the operation.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="document">Sorted cache document.</param>
        public void Save(string cachePath, EditorAssetHashCacheDocument document) {
            SaveCount++;
            LastSavedDocument = document;
            InnerStore.Save(cachePath, document);
        }

        /// <summary>
        /// Merges dirty entries through the real store after counting the operation.
        /// </summary>
        public EditorAssetHashCacheDocument Update(
            string cachePath,
            IReadOnlyDictionary<string, EditorAssetHashCacheEntry> updates,
            IReadOnlyCollection<string> removedPaths) {
            LastRemovedPaths = removedPaths.ToArray();
            if (FailNextUpdate) {
                FailNextUpdate = false;
                throw new IOException("Test cache store failure.");
            }

            SaveCount++;
            EditorAssetHashCacheDocument document = InnerStore.Update(cachePath, updates, removedPaths);
            LastSavedDocument = document;
            return document;
        }
    }

    /// <summary>
    /// Coordinates two cache writes after each owner has read the same stored document.
    /// </summary>
    sealed class OverlappingAssetHashCacheStore : IEditorAssetHashCacheStore {
        readonly Barrier SaveBarrier = new Barrier(2);
        readonly FileEditorAssetHashCacheStore InnerStore;

        public OverlappingAssetHashCacheStore(string projectRootPath) {
            InnerStore = new FileEditorAssetHashCacheStore(projectRootPath);
        }

        /// <summary>
        /// Loads the current cache document.
        /// </summary>
        public EditorAssetHashCacheDocument Load(string cachePath) {
            return InnerStore.Load(cachePath);
        }

        /// <summary>
        /// Saves after both owners have prepared their updates.
        /// </summary>
        public void Save(string cachePath, EditorAssetHashCacheDocument document) {
            SaveBarrier.SignalAndWait(TimeSpan.FromSeconds(10));
            InnerStore.Save(cachePath, document);
        }

        /// <summary>
        /// Coordinates two dirty updates before entering the atomic file-store operation.
        /// </summary>
        public EditorAssetHashCacheDocument Update(
            string cachePath,
            IReadOnlyDictionary<string, EditorAssetHashCacheEntry> updates,
            IReadOnlyCollection<string> removedPaths) {
            SaveBarrier.SignalAndWait(TimeSpan.FromSeconds(10));
            return InnerStore.Update(cachePath, updates, removedPaths);
        }
    }

    /// <summary>
    /// Counts path hashing calls while using the production SHA-256 implementation.
    /// </summary>
    sealed class CountingAssetFileHasher : AssetFileHasher {
        public CountingAssetFileHasher(string projectRootPath) : base(projectRootPath) {
        }
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
