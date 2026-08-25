namespace helengine.editor {
    /// <summary>
    /// Indexes authored asset identities and repairs duplicate UUID sidecars deterministically.
    /// </summary>
    public sealed class EditorAssetIdentityIndex {
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
        /// Current indexed entries by normalized path.
        /// </summary>
        readonly Dictionary<string, EditorAssetIdentityEntry> EntriesByPath;

        /// <summary>
        /// Current indexed entries by current UUID.
        /// </summary>
        readonly Dictionary<string, List<EditorAssetIdentityEntry>> EntriesByAssetId;

        /// <summary>
        /// Previous owner path by UUID for stable duplicate repair.
        /// </summary>
        readonly Dictionary<string, string> PreviousOwners;

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
            EntriesByPath = new Dictionary<string, EditorAssetIdentityEntry>(StringComparer.OrdinalIgnoreCase);
            EntriesByAssetId = new Dictionary<string, List<EditorAssetIdentityEntry>>(StringComparer.Ordinal);
            PreviousOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Refreshes the index from authored files and repairs duplicate current UUIDs.
        /// </summary>
        public void Refresh() {
            if (!Directory.Exists(AssetsRootPath)) {
                Directory.CreateDirectory(AssetsRootPath);
            }

            List<string> sourcePaths = Directory.EnumerateFiles(AssetsRootPath, "*", SearchOption.AllDirectories)
                .Where(path => PathClassifier.IsAuthoredAsset(path))
                .Select(Path.GetFullPath)
                .OrderBy(path => NormalizeRelativePath(path), StringComparer.Ordinal)
                .ToList();
            List<EditorAssetIdentityEntry> loadedEntries = new List<EditorAssetIdentityEntry>();
            for (int index = 0; index < sourcePaths.Count; index++) {
                string fullPath = sourcePaths[index];
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

                    AssetIdentityMetadataDocument repairedDocument = MetadataService.Load(duplicate.FullPath);
                    if (!repairedDocument.FormerAssetIds.Contains(group.Key, StringComparer.Ordinal)) {
                        repairedDocument.FormerAssetIds.Add(group.Key);
                    }
                    string replacementId = CreateUnusedAssetId(usedIds);
                    repairedDocument.AssetId = replacementId;
                    usedIds.Add(replacementId);
                    MetadataService.Save(duplicate.FullPath, repairedDocument);
                    for (int loadedIndex = 0; loadedIndex < loadedEntries.Count; loadedIndex++) {
                        if (string.Equals(loadedEntries[loadedIndex].FullPath, duplicate.FullPath, StringComparison.OrdinalIgnoreCase)) {
                            loadedEntries[loadedIndex] = CreateEntry(duplicate.FullPath, repairedDocument);
                            break;
                        }
                    }
                }
                PreviousOwners[group.Key] = owner.RelativePath;
            }

            EntriesByPath.Clear();
            EntriesByAssetId.Clear();
            for (int index = 0; index < loadedEntries.Count; index++) {
                EditorAssetIdentityEntry entry = loadedEntries[index];
                EntriesByPath[entry.RelativePath] = entry;
                List<EditorAssetIdentityEntry> assetEntries;
                if (!EntriesByAssetId.TryGetValue(entry.AssetId, out assetEntries)) {
                    assetEntries = new List<EditorAssetIdentityEntry>();
                    EntriesByAssetId[entry.AssetId] = assetEntries;
                }
                assetEntries.Add(entry);
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
            List<EditorAssetIdentityEntry> matches = new List<EditorAssetIdentityEntry>();
            if (string.IsNullOrWhiteSpace(assetId)) {
                return matches;
            }
            foreach (EditorAssetIdentityEntry entry in EntriesByPath.Values) {
                if (entry.EntryKind == expectedKind && (string.Equals(entry.AssetId, assetId, StringComparison.Ordinal) || entry.FormerAssetIds.Contains(assetId, StringComparer.Ordinal))) {
                    matches.Add(entry);
                }
            }
            matches.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal));
            return matches;
        }

        /// <summary>
        /// Enumerates all entries of one compatible asset kind.
        /// </summary>
        /// <param name="expectedKind">Required asset kind.</param>
        /// <returns>Sorted compatible entries.</returns>
        public IReadOnlyList<EditorAssetIdentityEntry> EnumerateCompatible(AssetEntryKind expectedKind) {
            return EntriesByPath.Values
                .Where(entry => entry.EntryKind == expectedKind)
                .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Determines whether a current UUID is owned by any indexed asset.
        /// </summary>
        /// <param name="assetId">UUID to inspect.</param>
        /// <returns>True when the current UUID is indexed.</returns>
        public bool IsCurrentAssetIdOwned(string assetId) {
            return !string.IsNullOrWhiteSpace(assetId) && EntriesByAssetId.ContainsKey(assetId);
        }

        /// <summary>
        /// Registers one authored path and refreshes duplicate ownership globally.
        /// </summary>
        /// <param name="fullPath">Absolute authored asset path.</param>
        /// <returns>Current indexed entry for the path.</returns>
        public EditorAssetIdentityEntry RegisterOrRefresh(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(fullPath));
            }
            if (!PathClassifier.IsAuthoredAsset(fullPath)) {
                throw new InvalidOperationException($"Path '{fullPath}' is not an authored asset.");
            }
            Refresh();
            return FindByPath(Path.GetRelativePath(AssetsRootPath, Path.GetFullPath(fullPath)));
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
                    if (string.Equals(candidates[index].RelativePath, previousOwnerPath, StringComparison.Ordinal)) {
                        return candidates[index];
                    }
                }
            }
            return candidates.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).First();
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
    }
}
