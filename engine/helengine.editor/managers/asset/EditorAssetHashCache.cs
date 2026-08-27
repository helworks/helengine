namespace helengine.editor {
    /// <summary>
    /// Caches authored asset SHA-256 values using source path fingerprints.
    /// </summary>
    public sealed class EditorAssetHashCache : IDisposable {
        /// <summary>
        /// Absolute project root path.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute assets root path.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Absolute disposable cache path.
        /// </summary>
        readonly string CacheFilePath;

        /// <summary>
        /// File hashing implementation.
        /// </summary>
        readonly AssetFileHasher FileHasher;

        /// <summary>
        /// Cache document store used for loading and deferred atomic persistence.
        /// </summary>
        readonly IEditorAssetHashCacheStore CacheStore;

        /// <summary>
        /// Shared classifier used to identify native payloads whose embedded identity is excluded from content hashes.
        /// </summary>
        readonly EditorAssetPathClassifier PathClassifier;

        /// <summary>
        /// Loaded cache entries keyed by normalized relative path.
        /// </summary>
        readonly Dictionary<string, EditorAssetHashCacheEntry> Entries;

        /// <summary>
        /// Tracks whether the cache has been loaded from disk.
        /// </summary>
        bool IsLoaded;

        /// <summary>
        /// Tracks whether in-memory entries differ from the last stored document.
        /// </summary>
        bool IsDirty;

        /// <summary>
        /// Relative paths changed by this cache since its last successful flush.
        /// </summary>
        readonly HashSet<string> DirtyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tracks whether this session-owned cache has been released.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes a project-scoped hash cache.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="fileHasher">Optional file hashing implementation.</param>
        public EditorAssetHashCache(string projectRootPath, AssetFileHasher fileHasher = null)
            : this(projectRootPath, fileHasher, new FileEditorAssetHashCacheStore()) {
        }

        /// <summary>
        /// Initializes a project-scoped hash cache with an instrumentable document store.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="fileHasher">Optional file hashing implementation.</param>
        /// <param name="cacheStore">Document store used for loading and flushing cache data.</param>
        internal EditorAssetHashCache(
            string projectRootPath,
            AssetFileHasher fileHasher,
            IEditorAssetHashCacheStore cacheStore) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheFilePath = Path.Combine(ProjectRootPath, "cache", "editor", "asset-identity-index.json");
            FileHasher = fileHasher ?? new AssetFileHasher();
            CacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
            PathClassifier = new EditorAssetPathClassifier();
            Entries = new Dictionary<string, EditorAssetHashCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the disposable cache file path.
        /// </summary>
        public string CachePath {
            get {
                EnsureNotDisposed();
                return CacheFilePath;
            }
        }

        /// <summary>
        /// Flushes dirty cache state and releases cache ownership exactly once.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            Flush();
            IsDisposed = true;
        }

        /// <summary>
        /// Atomically stores the sorted cache document when in-memory entries are dirty.
        /// </summary>
        public void Flush() {
            EnsureNotDisposed();
            if (!IsDirty) {
                return;
            }

            Save();
            IsDirty = false;
        }

        /// <summary>
        /// Gets the cached or freshly computed hash for one authored asset.
        /// </summary>
        /// <param name="assetPath">Absolute authored asset path.</param>
        /// <returns>Lowercase SHA-256 hash prefixed with <c>sha256:</c>.</returns>
        public string GetContentHash(string assetPath) {
            EnsureNotDisposed();
            string fullPath = NormalizeAndValidatePath(assetPath);
            FileInfo fileInfo = new FileInfo(fullPath);
            EnsureLoaded();
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, fullPath));
            EditorAssetHashCacheEntry cachedEntry;
            if (Entries.TryGetValue(relativePath, out cachedEntry) &&
                cachedEntry.Length == fileInfo.Length &&
                cachedEntry.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks &&
                IsValidContentHash(cachedEntry.ContentHash)) {
                return cachedEntry.ContentHash;
            }

            string contentHash = string.Concat("sha256:", ComputeContentHash(fullPath));
            Entries[relativePath] = new EditorAssetHashCacheEntry {
                RelativePath = relativePath,
                Length = fileInfo.Length,
                LastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
                ContentHash = contentHash
            };
            DirtyPaths.Add(relativePath);
            IsDirty = true;
            return contentHash;
        }

        /// <summary>
        /// Computes one recovery hash, canonicalizing native payloads without their embedded identity metadata.
        /// </summary>
        /// <param name="fullPath">Absolute authored source path.</param>
        /// <returns>Lowercase SHA-256 hex without the algorithm prefix.</returns>
        string ComputeContentHash(string fullPath) {
            if (!PathClassifier.UsesEmbeddedIdentity(fullPath)) {
                return FileHasher.ComputeHash(fullPath);
            }

            using FileStream input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(input);
            input.Position = 0;
            using MemoryStream canonical = new MemoryStream();
            if (header.RecordKind == (ushort)EditorBinaryRecordKind.Asset) {
                Asset asset = AssetSerializer.Deserialize(input);
                asset.AuthoringAssetId = string.Empty;
                asset.FormerAuthoringAssetIds = Array.Empty<string>();
                AssetSerializer.Serialize(canonical, asset);
            } else if (header.RecordKind == (ushort)EditorBinaryRecordKind.AssetImportSettings &&
                       header.ValueKind == (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument) {
                MaterialAssetCommonSettingsDocument material = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(input);
                material.AuthoringAssetId = string.Empty;
                material.FormerAuthoringAssetIds.Clear();
                MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(canonical, material);
            } else {
                throw new InvalidOperationException($"Engine-native asset '{fullPath}' does not expose canonical identity metadata.");
            }

            canonical.Position = 0;
            return FileHasher.ComputeHash(canonical);
        }

        /// <summary>
        /// Loads the disposable cache, discarding malformed data.
        /// </summary>
        void EnsureLoaded() {
            if (IsLoaded) {
                return;
            }

            IsLoaded = true;
            EditorAssetHashCacheDocument document = CacheStore.Load(CacheFilePath);
            if (document == null || document.Entries == null) {
                return;
            }
            for (int index = 0; index < document.Entries.Count; index++) {
                EditorAssetHashCacheEntry entry = document.Entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.RelativePath) && IsValidContentHash(entry.ContentHash)) {
                    Entries[NormalizeRelativePath(entry.RelativePath)] = entry;
                }
            }
        }

        /// <summary>
        /// Builds and atomically stores a sorted cache document.
        /// </summary>
        void Save() {
            // Reload the document at the store boundary so independent caches
            // retain updates made by another owner after this cache was loaded.
            Dictionary<string, EditorAssetHashCacheEntry> mergedEntries =
                new Dictionary<string, EditorAssetHashCacheEntry>(StringComparer.OrdinalIgnoreCase);
            EditorAssetHashCacheDocument storedDocument = CacheStore.Load(CacheFilePath);
            if (storedDocument != null && storedDocument.Entries != null) {
                for (int index = 0; index < storedDocument.Entries.Count; index++) {
                    EditorAssetHashCacheEntry entry = storedDocument.Entries[index];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.RelativePath) && IsValidContentHash(entry.ContentHash)) {
                        string relativePath = NormalizeRelativePath(entry.RelativePath);
                        mergedEntries[relativePath] = entry;
                    }
                }
            }

            foreach (string dirtyPath in DirtyPaths) {
                EditorAssetHashCacheEntry entry;
                if (Entries.TryGetValue(dirtyPath, out entry)) {
                    mergedEntries[dirtyPath] = entry;
                } else {
                    mergedEntries.Remove(dirtyPath);
                }
            }

            EditorAssetHashCacheDocument document = new EditorAssetHashCacheDocument {
                Entries = mergedEntries.Values.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToList()
            };
            CacheStore.Save(CacheFilePath, document);
            DirtyPaths.Clear();
        }

        /// <summary>
        /// Validates and normalizes one source path within the assets root.
        /// </summary>
        /// <param name="assetPath">Candidate asset path.</param>
        /// <returns>Normalized full path.</returns>
        string NormalizeAndValidatePath(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }
            string fullPath = Path.GetFullPath(assetPath);
            string assetsPrefix = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) {
                throw new InvalidOperationException($"Asset path '{assetPath}' is not an existing file inside the assets directory.");
            }
            return fullPath;
        }

        /// <summary>
        /// Normalizes one path to slash-separated relative form.
        /// </summary>
        /// <param name="relativePath">Path to normalize.</param>
        /// <returns>Normalized relative path.</returns>
        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Validates one cached hash value.
        /// </summary>
        /// <param name="contentHash">Candidate cached hash.</param>
        /// <returns>True when the value is a prefixed lowercase SHA-256 hash.</returns>
        static bool IsValidContentHash(string contentHash) {
            if (string.IsNullOrWhiteSpace(contentHash) || contentHash.Length != 71 || !contentHash.StartsWith("sha256:", StringComparison.Ordinal)) {
                return false;
            }
            for (int index = 7; index < contentHash.Length; index++) {
                char character = contentHash[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Rejects operations after this cache has released its owned state.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorAssetHashCache));
            }
        }
    }
}
