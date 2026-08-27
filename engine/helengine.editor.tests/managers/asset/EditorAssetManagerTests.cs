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
        EditorAssetManager manager = new EditorAssetManager(TempRootPath);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

        manager.LoadEntries(entries);

        Assert.Contains(entries, entry => entry.Name == "Visible.png");
        Assert.DoesNotContain(entries, entry => entry.Name == "Visible.png.hmeta");
    }

    /// <summary>
    /// Ensures the asset manager flushes its owned hash cache at its lifetime boundary.
    /// </summary>
    [Fact]
    public void Dispose_FlushesOwnedIdentityHashCache() {
        string assetPath = Path.Combine(TempRootPath, "assets", "Visible.png");
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        EditorAssetManager manager = new EditorAssetManager(TempRootPath);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

        manager.LoadEntries(entries);
        string cachePath = Path.Combine(TempRootPath, "cache", "editor", "asset-identity-index.json");
        Assert.False(File.Exists(cachePath));

        manager.Dispose();
        manager.Dispose();

        Assert.True(File.Exists(cachePath));
    }

    /// <summary>
    /// Ensures a failed owned-cache flush leaves the manager retryable instead of losing ownership state.
    /// </summary>
    [Fact]
    public void Dispose_WhenOwnedCacheFlushFails_RetriesOnNextDispose() {
        string assetPath = Path.Combine(TempRootPath, "assets", "Retry.png");
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        FailOnceAssetHashCacheStore store = new FailOnceAssetHashCacheStore();
        using EditorAssetHashCache cache = new EditorAssetHashCache(TempRootPath, new AssetFileHasher(), store);
        EditorAssetManager manager = new EditorAssetManager(TempRootPath, cache, true);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

        manager.LoadEntries(entries);

        Assert.Throws<IOException>(() => manager.Dispose());
        manager.Dispose();

        Assert.Equal(2, store.UpdateAttempts);
    }

    /// <summary>
    /// Ensures one malformed sidecar is preserved and reported without hiding later valid assets.
    /// </summary>
    [Fact]
    public void LoadEntries_WhenSidecarIsMalformed_PreservesItAndContinuesDirectoryEnumeration() {
        string malformedAssetPath = Path.Combine(TempRootPath, "assets", "A-Malformed.png");
        string validAssetPath = Path.Combine(TempRootPath, "assets", "B-Visible.png");
        File.WriteAllBytes(malformedAssetPath, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(validAssetPath, new byte[] { 4, 5, 6 });
        string metadataPath = malformedAssetPath + ".hmeta";
        const string malformedMetadata = "{}";
        File.WriteAllText(metadataPath, malformedMetadata);
        EditorAssetManager manager = new EditorAssetManager(TempRootPath);
        List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
        List<string> errors = new List<string>();
        void CaptureError(LogEntry entry) => errors.Add(entry.Message);

        Logger.ErrorLogged += CaptureError;
        try {
            manager.LoadEntries(entries);
        } finally {
            Logger.ErrorLogged -= CaptureError;
        }

        Assert.Equal(malformedMetadata, File.ReadAllText(metadataPath));
        Assert.DoesNotContain(entries, entry => entry.Name == "A-Malformed.png");
        Assert.Contains(entries, entry => entry.Name == "B-Visible.png");
        Assert.Contains(errors, message => message.Contains("A-Malformed.png", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ensures the shared classifier distinguishes metadata sidecars from authored material sidecars.
    /// </summary>
    [Fact]
    public void EditorAssetPathClassifier_HidesMetadataAndUnrecognizedImporterSidecars() {
        EditorAssetPathClassifier classifier = new EditorAssetPathClassifier();
        string metadataPath = Path.Combine(TempRootPath, "assets", "Texture.png.hmeta");
        string importerPath = Path.Combine(TempRootPath, "assets", "Texture.png.hasset");
        string materialPath = Path.Combine(TempRootPath, "assets", "materials", "Material.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);
        string importedMaterialPath = Path.Combine(TempRootPath, "assets", "models", "Model", "Material.hasset");
        Directory.CreateDirectory(Path.GetDirectoryName(importedMaterialPath)!);
        using (FileStream stream = File.Create(materialPath)) {
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                global::helengine.files.EditorAssetBinarySerializer.CurrentVersion,
                global::helengine.files.EditorAssetBinarySerializer.FormatId,
                (ushort)EditorBinaryRecordKind.Asset,
                (ushort)EditorAssetBinaryValueKind.MaterialAsset));
        }
        using (FileStream stream = File.Create(importedMaterialPath)) {
            EngineBinaryHeaderSerializer.Write(stream, new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                global::helengine.files.EditorAssetBinarySerializer.CurrentVersion,
                global::helengine.files.EditorAssetBinarySerializer.FormatId,
                (ushort)EditorBinaryRecordKind.AssetImportSettings,
                (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument));
        }

        Assert.True(classifier.ShouldHide(metadataPath));
        Assert.True(classifier.ShouldHide(importerPath));
        Assert.False(classifier.ShouldHide(materialPath));
        Assert.True(classifier.ShouldHide(importedMaterialPath));
    }

    /// <summary>
    /// Fails the first cache update while delegating all subsequent updates to the file store.
    /// </summary>
    sealed class FailOnceAssetHashCacheStore : IEditorAssetHashCacheStore {
        readonly FileEditorAssetHashCacheStore innerStore = new FileEditorAssetHashCacheStore();

        public int UpdateAttempts { get; private set; }

        public EditorAssetHashCacheDocument Load(string cachePath) {
            return innerStore.Load(cachePath);
        }

        public void Save(string cachePath, EditorAssetHashCacheDocument document) {
            innerStore.Save(cachePath, document);
        }

        public EditorAssetHashCacheDocument Update(
            string cachePath,
            IReadOnlyDictionary<string, EditorAssetHashCacheEntry> updates,
            IReadOnlyCollection<string> removedPaths) {
            UpdateAttempts++;
            if (UpdateAttempts == 1) {
                throw new IOException("Test cache flush failure.");
            }
            return innerStore.Update(cachePath, updates, removedPaths);
        }
    }
}
