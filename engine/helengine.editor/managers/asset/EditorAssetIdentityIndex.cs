namespace helengine.editor {
    /// <summary>
    /// Indexes authored asset identities and repairs duplicate UUID sidecars deterministically.
    /// </summary>
    public sealed class EditorAssetIdentityIndex : IDisposable {
        /// <summary>
        /// Absolute project root path.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute assets root path.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Sidecar persistence service.
        /// </summary>
        readonly AssetIdentityMetadataService MetadataService;

        /// <summary>
        /// Shared authored file classifier.
        /// </summary>
        readonly EditorAssetPathClassifier PathClassifier;

        /// <summary>
        /// Project-scoped content hash cache.
        /// </summary>
        readonly EditorAssetHashCache HashCache;
        /// <summary>
        /// Indicates whether this index created and owns its hash cache.
        /// </summary>
        readonly bool OwnsHashCache;

        /// <summary>
        /// Filesystem catalog used for explicit authored-file reconciliation.
        /// </summary>
        readonly IEditorAssetFileCatalog FileCatalog;

        /// <summary>
        /// Current indexed entries by normalized path.
        /// </summary>
        readonly Dictionary<string, EditorAssetIdentityEntry> EntriesByPath;

        /// <summary>
        /// Current indexed entries by current UUID.
        /// </summary>
        readonly Dictionary<string, List<EditorAssetIdentityEntry>> EntriesByAssetId;

        /// <summary>
        /// Current and former identities mapped to their indexed entries for constant-time reference recovery.
        /// </summary>
        readonly Dictionary<string, List<EditorAssetIdentityEntry>> EntriesByLookupIdentity;

        /// <summary>
        /// Previous owner path by UUID for stable duplicate repair.
        /// </summary>
        readonly Dictionary<string, string> PreviousOwners;

        /// <summary>
        /// Authored paths whose external identity document was absent during the last reconciliation.
        /// </summary>
        readonly HashSet<string> MissingMetadataPaths;

        /// <summary>
        /// Tracks whether the initial authored-file snapshot has been built.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Tracks whether this index has released its owned resources.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes a project-scoped identity index.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="metadataService">Optional metadata service.</param>
        /// <param name="pathClassifier">Optional shared classifier.</param>
        /// <param name="hashCache">Optional hash cache.</param>
        public EditorAssetIdentityIndex(
            string projectRootPath,
            AssetIdentityMetadataService metadataService = null,
            EditorAssetPathClassifier pathClassifier = null,
            EditorAssetHashCache hashCache = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            MetadataService = metadataService ?? new AssetIdentityMetadataService();
            PathClassifier = pathClassifier ?? new EditorAssetPathClassifier();
            HashCache = hashCache ?? new EditorAssetHashCache(ProjectRootPath);
            OwnsHashCache = hashCache == null;
            FileCatalog = new FileEditorAssetFileCatalog();
            EntriesByPath = new Dictionary<string, EditorAssetIdentityEntry>(PathComparer);
            EntriesByAssetId = new Dictionary<string, List<EditorAssetIdentityEntry>>(StringComparer.Ordinal);
            EntriesByLookupIdentity = new Dictionary<string, List<EditorAssetIdentityEntry>>(StringComparer.Ordinal);
            PreviousOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            MissingMetadataPaths = new HashSet<string>(PathComparer);
        }

        /// <summary>
        /// Initializes one index with a testable authored-file catalog.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="metadataService">Optional metadata service.</param>
        /// <param name="pathClassifier">Optional shared classifier.</param>
        /// <param name="hashCache">Optional hash cache.</param>
        /// <param name="fileCatalog">Catalog used to enumerate authored files.</param>
        internal EditorAssetIdentityIndex(
            string projectRootPath,
            AssetIdentityMetadataService metadataService,
            EditorAssetPathClassifier pathClassifier,
            EditorAssetHashCache hashCache,
            IEditorAssetFileCatalog fileCatalog)
            : this(projectRootPath, metadataService, pathClassifier, hashCache) {
            FileCatalog = fileCatalog ?? throw new ArgumentNullException(nameof(fileCatalog));
        }

        /// <summary>
        /// Initializes the index from authored files once and repairs duplicate current UUIDs.
        /// </summary>
        public void Initialize() {
            EnsureNotDisposed();
            if (IsInitialized) {
                return;
            }

            ReconcileCore();
            IsInitialized = true;
        }

        /// <summary>
        /// Reconciles external authored-file changes through one explicit full enumeration.
        /// </summary>
        public void ReconcileExternalChanges() {
            EnsureNotDisposed();
            EnsureInitialized();
            ReconcileCore();
        }

        /// <summary>
        /// Indicates whether one external path lacked identity metadata during the last reconciliation.
        /// </summary>
        /// <param name="fullPath">Absolute authored asset path.</param>
        /// <returns>True when external metadata was absent at reconciliation time.</returns>
        internal bool WasMetadataMissing(string fullPath) {
            EnsureNotDisposed();
            return !string.IsNullOrWhiteSpace(fullPath) && MissingMetadataPaths.Contains(Path.GetFullPath(fullPath));
        }

        /// <summary>
        /// Copies paths that lacked external identity metadata at the last reconciliation.
        /// </summary>
        /// <returns>Independent set of paths with missing metadata.</returns>
        internal HashSet<string> CopyMissingMetadataPaths() {
            EnsureNotDisposed();
            return new HashSet<string>(MissingMetadataPaths, PathComparer);
        }

        /// <summary>
        /// Returns the cache used by this index so another project service can borrow its lifetime.
        /// </summary>
        internal EditorAssetHashCache HashCacheValue {
            get {
                EnsureNotDisposed();
                return HashCache;
            }
        }

        /// <summary>
        /// Releases a hash cache created by this index; injected caches remain caller-owned.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            if (OwnsHashCache) {
                HashCache.Dispose();
            }
            IsDisposed = true;
        }

        /// <summary>
        /// Reconciles the current authored-file snapshot and rebuilds all lookup maps.
        /// </summary>
        void ReconcileCore() {
            if (!Directory.Exists(AssetsRootPath)) {
                Directory.CreateDirectory(AssetsRootPath);
            }
            ValidateNoReparseTraversal(AssetsRootPath);

            MissingMetadataPaths.Clear();
            List<string> sourcePaths = FileCatalog.EnumerateFiles(AssetsRootPath)
                .Select(Path.GetFullPath)
                .OrderBy(path => NormalizeRelativePath(path), PathComparer)
                .ThenBy(path => NormalizeRelativePath(path), StringComparer.Ordinal)
                .ToList();
            List<EditorAssetIdentityEntry> loadedEntries = new List<EditorAssetIdentityEntry>();
            for (int index = 0; index < sourcePaths.Count; index++) {
                string fullPath = sourcePaths[index];
                ValidateNoReparseTraversal(fullPath);
                if (!PathClassifier.IsAuthoredAsset(fullPath)) {
                    continue;
                }
                if (!PathClassifier.UsesEmbeddedIdentity(fullPath) && !File.Exists(fullPath + ".hmeta")) {
                    MissingMetadataPaths.Add(fullPath);
                }
                AssetIdentityMetadataDocument document = LoadIdentityMetadata(fullPath);
                loadedEntries.Add(CreateEntry(fullPath, document));
            }

            Dictionary<string, List<EditorAssetIdentityEntry>> duplicateGroups = GroupByCurrentAssetId(loadedEntries);
            HashSet<string> usedIds = new HashSet<string>(loadedEntries.Select(entry => entry.AssetId), StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<EditorAssetIdentityEntry>> group in duplicateGroups) {
                if (group.Value.Count < 2) {
                    continue;
                }

                EditorAssetIdentityEntry owner = SelectOwner(group.Key, group.Value);
                for (int index = 0; index < group.Value.Count; index++) {
                    EditorAssetIdentityEntry duplicate = group.Value[index];
                    if (ReferenceEquals(duplicate, owner)) {
                        continue;
                    }

                    // Native authored payloads intentionally retain their embedded identity. Multiple
                    // authored files may share an explicit identity, and the resolver selects the
                    // saved path (or ordinal path) without rewriting either payload.
                    if (PathClassifier.UsesEmbeddedIdentity(duplicate.FullPath)) {
                        continue;
                    }

                    AssetIdentityMetadataDocument repairedDocument = MetadataService.Load(duplicate.FullPath);
                    if (!repairedDocument.FormerAssetIds.Contains(group.Key, StringComparer.Ordinal)) {
                        repairedDocument.FormerAssetIds.Add(group.Key);
                    }
                    string replacementId = CreateUnusedAssetId(usedIds);
                    repairedDocument.AssetId = replacementId;
                    usedIds.Add(replacementId);
                    MetadataService.Save(duplicate.FullPath, repairedDocument);
                    for (int loadedIndex = 0; loadedIndex < loadedEntries.Count; loadedIndex++) {
                        if (string.Equals(loadedEntries[loadedIndex].FullPath, duplicate.FullPath, PathComparison)) {
                            loadedEntries[loadedIndex] = CreateEntry(duplicate.FullPath, repairedDocument);
                            break;
                        }
                    }
                }
                PreviousOwners[group.Key] = owner.RelativePath;
            }

            EntriesByPath.Clear();
            EntriesByAssetId.Clear();
            EntriesByLookupIdentity.Clear();
            for (int index = 0; index < loadedEntries.Count; index++) {
                EditorAssetIdentityEntry entry = loadedEntries[index];
                AddEntry(entry);
                if (!PreviousOwners.ContainsKey(entry.AssetId)) {
                    PreviousOwners[entry.AssetId] = entry.RelativePath;
                }
            }
        }

        /// <summary>
        /// Finds one indexed entry by normalized assets-relative path.
        /// </summary>
        /// <param name="relativePath">Assets-relative path.</param>
        /// <returns>Matching entry, or null when absent.</returns>
        public EditorAssetIdentityEntry FindByPath(string relativePath) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return null;
            }
            EditorAssetIdentityEntry entry;
            EntriesByPath.TryGetValue(NormalizeRelativePath(relativePath), out entry);
            return entry;
        }

        /// <summary>
        /// Finds entries matching a current or former UUID and expected asset kind.
        /// </summary>
        /// <param name="assetId">Current or former UUID.</param>
        /// <param name="expectedKind">Required asset kind.</param>
        /// <returns>Matching indexed entries.</returns>
        public IReadOnlyList<EditorAssetIdentityEntry> FindByAssetId(string assetId, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            List<EditorAssetIdentityEntry> matches = new List<EditorAssetIdentityEntry>();
            if (string.IsNullOrWhiteSpace(assetId)) {
                return matches;
            }
            List<EditorAssetIdentityEntry> indexedEntries;
            if (!EntriesByLookupIdentity.TryGetValue(assetId, out indexedEntries)) {
                return matches;
            }

            for (int index = 0; index < indexedEntries.Count; index++) {
                EditorAssetIdentityEntry entry = indexedEntries[index];
                if (entry.EntryKind == expectedKind) {
                    matches.Add(entry);
                }
            }
            matches.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
            return matches;
        }

        /// <summary>
        /// Enumerates all entries of one compatible asset kind.
        /// </summary>
        /// <param name="expectedKind">Required asset kind.</param>
        /// <returns>Sorted compatible entries.</returns>
        public IReadOnlyList<EditorAssetIdentityEntry> EnumerateCompatible(AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            return EntriesByPath.Values
                .Where(entry => entry.EntryKind == expectedKind)
                .OrderBy(entry => entry.RelativePath, PathComparer)
                .ToList();
        }

        /// <summary>
        /// Determines whether a current UUID is owned by any indexed asset.
        /// </summary>
        /// <param name="assetId">UUID to inspect.</param>
        /// <returns>True when the current UUID is indexed.</returns>
        public bool IsCurrentAssetIdOwned(string assetId) {
            EnsureNotDisposed();
            return !string.IsNullOrWhiteSpace(assetId) && EntriesByAssetId.ContainsKey(assetId);
        }

        /// <summary>
        /// Registers or updates one authored path without enumerating the assets tree.
        /// </summary>
        /// <param name="fullPath">Absolute authored asset path.</param>
        /// <returns>Current indexed entry for the path.</returns>
        public EditorAssetIdentityEntry RegisterOrUpdate(string fullPath) {
            EnsureNotDisposed();
            string normalizedFullPath = NormalizeAndValidateAssetsPath(fullPath);
            EnsureInitialized();
            ValidateNoReparseTraversal(normalizedFullPath);
            if (!PathClassifier.IsAuthoredAsset(normalizedFullPath)) {
                throw new InvalidOperationException($"Path '{fullPath}' is not an authored asset.");
            }
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, normalizedFullPath));
            EditorAssetIdentityEntry existingEntry;
            if (EntriesByPath.TryGetValue(relativePath, out existingEntry)) {
                RemoveEntry(existingEntry);
            }

            if (!PathClassifier.UsesEmbeddedIdentity(normalizedFullPath) && !File.Exists(normalizedFullPath + ".hmeta")) {
                MissingMetadataPaths.Add(normalizedFullPath);
            } else {
                MissingMetadataPaths.Remove(normalizedFullPath);
            }
            EditorAssetIdentityEntry entry = CreateEntry(normalizedFullPath, LoadIdentityMetadata(normalizedFullPath));
            AddEntry(entry);
            if (!PreviousOwners.ContainsKey(entry.AssetId)) {
                PreviousOwners[entry.AssetId] = entry.RelativePath;
            }

            return entry;
        }

        /// <summary>
        /// Removes one indexed authored path without enumerating the assets tree.
        /// </summary>
        /// <param name="fullPath">Absolute authored asset path.</param>
        public void Remove(string fullPath) {
            EnsureNotDisposed();
            string normalizedFullPath = NormalizeAndValidateAssetsPath(fullPath);
            EnsureInitialized();
            ValidateNoReparseTraversal(normalizedFullPath);
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, normalizedFullPath));
            EditorAssetIdentityEntry existingEntry;
            if (EntriesByPath.TryGetValue(relativePath, out existingEntry)) {
                RemoveEntry(existingEntry);
            }
            MissingMetadataPaths.Remove(normalizedFullPath);
        }

        /// <summary>
        /// Adds one entry to all maintained lookup maps.
        /// </summary>
        /// <param name="entry">Entry to index.</param>
        void AddEntry(EditorAssetIdentityEntry entry) {
            EntriesByPath[entry.RelativePath] = entry;
            AddToLookup(EntriesByAssetId, entry.AssetId, entry);
            AddToLookup(EntriesByLookupIdentity, entry.AssetId, entry);
            for (int index = 0; index < entry.FormerAssetIds.Count; index++) {
                AddToLookup(EntriesByLookupIdentity, entry.FormerAssetIds[index], entry);
            }
        }

        /// <summary>
        /// Removes one entry from all maintained lookup maps.
        /// </summary>
        /// <param name="entry">Entry to remove.</param>
        void RemoveEntry(EditorAssetIdentityEntry entry) {
            EntriesByPath.Remove(entry.RelativePath);
            RemoveFromLookup(EntriesByAssetId, entry.AssetId, entry);
            RemoveFromLookup(EntriesByLookupIdentity, entry.AssetId, entry);
            for (int index = 0; index < entry.FormerAssetIds.Count; index++) {
                RemoveFromLookup(EntriesByLookupIdentity, entry.FormerAssetIds[index], entry);
            }
        }

        /// <summary>
        /// Adds one entry to a lookup map without creating duplicate list members.
        /// </summary>
        static void AddToLookup(Dictionary<string, List<EditorAssetIdentityEntry>> lookup, string identity, EditorAssetIdentityEntry entry) {
            List<EditorAssetIdentityEntry> entries;
            if (!lookup.TryGetValue(identity, out entries)) {
                entries = new List<EditorAssetIdentityEntry>();
                lookup[identity] = entries;
            }
            if (!entries.Contains(entry)) {
                entries.Add(entry);
            }
        }

        /// <summary>
        /// Removes one entry from a lookup map.
        /// </summary>
        static void RemoveFromLookup(Dictionary<string, List<EditorAssetIdentityEntry>> lookup, string identity, EditorAssetIdentityEntry entry) {
            List<EditorAssetIdentityEntry> entries;
            if (!lookup.TryGetValue(identity, out entries)) {
                return;
            }
            entries.Remove(entry);
            if (entries.Count == 0) {
                lookup.Remove(identity);
            }
        }

        /// <summary>
        /// Creates one immutable index entry from a source path and validated metadata.
        /// </summary>
        /// <param name="fullPath">Absolute source path.</param>
        /// <param name="document">Validated metadata.</param>
        /// <returns>Index entry.</returns>
        EditorAssetIdentityEntry CreateEntry(string fullPath, AssetIdentityMetadataDocument document) {
            return new EditorAssetIdentityEntry(
                fullPath,
                NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, fullPath)),
                PathClassifier.Classify(fullPath),
                document);
        }

        /// <summary>
        /// Loads one current identity, creating metadata only when an external sidecar is absent.
        /// </summary>
        /// <param name="fullPath">Absolute authored source path.</param>
        /// <returns>Validated identity metadata.</returns>
        AssetIdentityMetadataDocument LoadIdentityMetadata(string fullPath) {
            if (PathClassifier.UsesEmbeddedIdentity(fullPath)) {
                return MetadataService.Load(fullPath);
            }
            return MetadataService.LoadOrCreate(fullPath, string.Empty);
        }

        /// <summary>
        /// Groups loaded entries by current UUID.
        /// </summary>
        /// <param name="entries">Loaded entries.</param>
        /// <returns>Duplicate groups keyed by current UUID.</returns>
        static Dictionary<string, List<EditorAssetIdentityEntry>> GroupByCurrentAssetId(IReadOnlyList<EditorAssetIdentityEntry> entries) {
            Dictionary<string, List<EditorAssetIdentityEntry>> groups = new Dictionary<string, List<EditorAssetIdentityEntry>>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Count; index++) {
                EditorAssetIdentityEntry entry = entries[index];
                List<EditorAssetIdentityEntry> group;
                if (!groups.TryGetValue(entry.AssetId, out group)) {
                    group = new List<EditorAssetIdentityEntry>();
                    groups[entry.AssetId] = group;
                }
                group.Add(entry);
            }
            return groups;
        }

        /// <summary>
        /// Selects the duplicate owner using prior ownership, then ordinal path order.
        /// </summary>
        /// <param name="assetId">Duplicated UUID.</param>
        /// <param name="candidates">Duplicate candidates.</param>
        /// <returns>Selected owner.</returns>
        EditorAssetIdentityEntry SelectOwner(string assetId, IReadOnlyList<EditorAssetIdentityEntry> candidates) {
            string previousOwnerPath;
            if (PreviousOwners.TryGetValue(assetId, out previousOwnerPath)) {
                for (int index = 0; index < candidates.Count; index++) {
                    if (string.Equals(candidates[index].RelativePath, previousOwnerPath, PathComparison)) {
                        return candidates[index];
                    }
                }
            }
            return candidates.OrderBy(entry => entry.RelativePath, PathComparer).First();
        }

        /// <summary>
        /// Creates a UUID not currently owned by any indexed entry.
        /// </summary>
        /// <param name="usedIds">Used UUID set.</param>
        /// <returns>Fresh lowercase UUID.</returns>
        static string CreateUnusedAssetId(ISet<string> usedIds) {
            string candidate;
            do {
                candidate = Guid.NewGuid().ToString("N");
            } while (usedIds.Contains(candidate));
            return candidate;
        }

        /// <summary>
        /// Normalizes one assets-relative path.
        /// </summary>
        /// <param name="path">Path to normalize.</param>
        /// <returns>Slash-separated relative path.</returns>
        static string NormalizeRelativePath(string path) {
            return path.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Rejects incremental operations before the initial snapshot exists.
        /// </summary>
        void EnsureInitialized() {
            if (!IsInitialized) {
                throw new InvalidOperationException("The asset identity index must be initialized before incremental operations.");
            }
        }

        /// <summary>
        /// Normalizes a path and verifies that it is strictly beneath the assets root.
        /// </summary>
        string NormalizeAndValidateAssetsPath(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(fullPath));
            }

            string normalizedFullPath = Path.GetFullPath(fullPath);
            string assetsPrefix = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!normalizedFullPath.StartsWith(assetsPrefix, PathComparison)) {
                throw new InvalidOperationException($"Path '{fullPath}' must be inside the assets directory.");
            }
            return normalizedFullPath;
        }

        /// <summary>
        /// Gets the operating-system path comparison used for containment.
        /// </summary>
        static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        /// <summary>
        /// Gets the operating-system path-key comparer used for indexed paths.
        /// </summary>
        static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// Rejects a linked or junctioned path before any classifier, metadata, or index access.
        /// </summary>
        void ValidateNoReparseTraversal(string fullPath) {
            string rootPath = Path.GetFullPath(AssetsRootPath);
            string currentPath = Path.GetFullPath(fullPath);
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Path '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }

                if (string.Equals(currentPath, rootPath, PathComparison)) {
                    return;
                }

                string parentPath = Path.GetDirectoryName(currentPath);
                string rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    (!string.Equals(parentPath, rootPath, PathComparison) && !parentPath.StartsWith(rootPrefix, PathComparison))) {
                    throw new InvalidOperationException($"Path '{fullPath}' must be inside the assets directory.");
                }
                currentPath = parentPath;
            }
        }

        /// <summary>
        /// Rejects operations after this index has released its owned resources.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorAssetIdentityIndex));
            }
        }
    }
}
