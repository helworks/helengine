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
        /// Session report receiving automatic identity repairs.
        /// </summary>
        readonly EditorAssetRepairReport RepairReport;

        /// <summary>
        /// Project publication marker used to make identity repairs observable to other sessions.
        /// </summary>
        IEditorProjectWriteChangeLog ChangeLog;

        /// <summary>
        /// Test-only mutation hook used to verify repair-batch rollback.
        /// </summary>
        Action<int> RepairMutationHook;

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
            EditorAssetHashCache hashCache = null,
            EditorAssetRepairReport repairReport = null) {
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
            RepairReport = repairReport ?? new EditorAssetRepairReport();
            ChangeLog = new FileEditorProjectWriteChangeLog(ProjectRootPath);
            RepairMutationHook = null;
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
            IEditorAssetFileCatalog fileCatalog,
            EditorAssetRepairReport repairReport = null,
            IEditorProjectWriteChangeLog changeLog = null,
            Action<int> repairMutationHook = null)
            : this(projectRootPath, metadataService, pathClassifier, hashCache, repairReport) {
            FileCatalog = fileCatalog ?? throw new ArgumentNullException(nameof(fileCatalog));
            ChangeLog = changeLog ?? new FileEditorProjectWriteChangeLog(ProjectRootPath);
            RepairMutationHook = repairMutationHook;
        }

        /// <summary>
        /// Initializes the index from authored files once and repairs duplicate current UUIDs.
        /// </summary>
        public void Initialize() {
            EnsureNotDisposed();
            if (IsInitialized) {
                return;
            }

            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReconcileCore();
            IsInitialized = true;
        }

        /// <summary>
        /// Reconciles external authored-file changes through one explicit full enumeration.
        /// </summary>
        public void ReconcileExternalChanges() {
            EnsureNotDisposed();
            EnsureInitialized();
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReconcileCore();
        }

        /// <summary>
        /// Reconciles authored files while the caller already owns the project publication lock.
        /// </summary>
        internal void ReconcileExternalChangesUnderLock() {
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
        /// Gets the report shared by this index and its project resolver.
        /// </summary>
        internal EditorAssetRepairReport RepairReportValue {
            get {
                EnsureNotDisposed();
                return RepairReport;
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
            HashSet<string> previousMissingMetadataPaths = new HashSet<string>(MissingMetadataPaths, PathComparer);
            Dictionary<string, string> previousOwners = new Dictionary<string, string>(PreviousOwners, StringComparer.Ordinal);
            try {
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
                Dictionary<string, AssetIdentityMetadataDocument> documentsByPath = new Dictionary<string, AssetIdentityMetadataDocument>(PathComparer);
                List<PendingIdentityRepair> pendingRepairs = new List<PendingIdentityRepair>();
                for (int index = 0; index < sourcePaths.Count; index++) {
                    string fullPath = sourcePaths[index];
                    ValidateNoReparseTraversal(fullPath);
                    if (!PathClassifier.IsAuthoredAsset(fullPath)) {
                        continue;
                    }
                    bool metadataCreated;
                    AssetIdentityMetadataDocument document = LoadIdentityMetadataForReconciliation(fullPath, out metadataCreated);
                    documentsByPath[fullPath] = document;
                    if (metadataCreated) {
                        MissingMetadataPaths.Add(fullPath);
                        pendingRepairs.Add(new PendingIdentityRepair(
                            AssetsRootPath,
                            fullPath,
                            document,
                            new EditorAssetRepairRecord(
                                EditorAssetRepairKind.MissingExternalMetadataCreation,
                                NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, fullPath)),
                                string.Empty,
                                document.AssetId,
                                null,
                                "external identity document was missing",
                                fullPath + ".hmeta",
                                "Created missing external asset identity metadata.")));
                    }
                    loadedEntries.Add(CreateEntry(fullPath, document));
                }

                Dictionary<string, List<EditorAssetIdentityEntry>> duplicateGroups = GroupByCurrentAssetId(loadedEntries);
                HashSet<string> usedIds = new HashSet<string>(loadedEntries.Select(entry => entry.AssetId), StringComparer.Ordinal);
                Dictionary<string, string> nextPreviousOwners = new Dictionary<string, string>(PreviousOwners, StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<EditorAssetIdentityEntry>> group in duplicateGroups) {
                    if (group.Value.Count < 2) {
                        continue;
                    }

                    EditorAssetIdentityEntry owner = SelectOwner(group.Key, group.Value, out bool selectedByRecordedOwner);
                    nextPreviousOwners[group.Key] = owner.RelativePath;
                    for (int index = 0; index < group.Value.Count; index++) {
                        EditorAssetIdentityEntry duplicate = group.Value[index];
                        if (ReferenceEquals(duplicate, owner)) {
                            continue;
                        }

                        AssetIdentityMetadataDocument repairedDocument = CloneDocument(documentsByPath[duplicate.FullPath]);
                        if (!repairedDocument.FormerAssetIds.Contains(group.Key, StringComparer.Ordinal)) {
                            repairedDocument.FormerAssetIds.Add(group.Key);
                        }
                        string replacementId = CreateUnusedAssetId(usedIds);
                        repairedDocument.AssetId = replacementId;
                        usedIds.Add(replacementId);
                        documentsByPath[duplicate.FullPath] = repairedDocument;
                        pendingRepairs.Add(new PendingIdentityRepair(
                            AssetsRootPath,
                            duplicate.FullPath,
                            repairedDocument,
                            new EditorAssetRepairRecord(
                                EditorAssetRepairKind.DuplicateIdReassignment,
                                duplicate.RelativePath,
                                group.Key,
                                replacementId,
                                null,
                                selectedByRecordedOwner
                                    ? $"selected recorded owner path='{owner.RelativePath}'"
                                    : $"selected ordinal owner path='{owner.RelativePath}'",
                                PathClassifier.UsesEmbeddedIdentity(duplicate.FullPath) ? duplicate.FullPath : duplicate.FullPath + ".hmeta",
                                "Reassigned copied identity to the non-owning asset.")));
                        for (int loadedIndex = 0; loadedIndex < loadedEntries.Count; loadedIndex++) {
                            if (string.Equals(loadedEntries[loadedIndex].FullPath, duplicate.FullPath, PathComparison)) {
                                loadedEntries[loadedIndex] = CreateEntry(duplicate.FullPath, repairedDocument);
                                break;
                            }
                        }
                    }
                }

                ApplyRepairBatch(pendingRepairs);

                EntriesByPath.Clear();
                EntriesByAssetId.Clear();
                EntriesByLookupIdentity.Clear();
                PreviousOwners.Clear();
                foreach (KeyValuePair<string, string> owner in nextPreviousOwners) {
                    PreviousOwners[owner.Key] = owner.Value;
                }
                for (int index = 0; index < loadedEntries.Count; index++) {
                    EditorAssetIdentityEntry entry = loadedEntries[index];
                    AddEntry(entry);
                    if (!PreviousOwners.ContainsKey(entry.AssetId)) {
                        PreviousOwners[entry.AssetId] = entry.RelativePath;
                    }
                }
            } catch {
                MissingMetadataPaths.Clear();
                foreach (string path in previousMissingMetadataPaths) {
                    MissingMetadataPaths.Add(path);
                }
                PreviousOwners.Clear();
                foreach (KeyValuePair<string, string> owner in previousOwners) {
                    PreviousOwners[owner.Key] = owner.Value;
                }
                throw;
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
        /// Determines whether one indexed path is the recorded owner of an identity.
        /// </summary>
        /// <param name="assetId">Identity to inspect.</param>
        /// <param name="relativePath">Normalized relative path to compare.</param>
        /// <returns>True when the path is the remembered owner.</returns>
        internal bool IsRecordedOwner(string assetId, string relativePath) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(assetId) || string.IsNullOrWhiteSpace(relativePath)) {
                return false;
            }

            return PreviousOwners.TryGetValue(assetId, out string ownerPath) &&
                string.Equals(ownerPath, NormalizeRelativePath(relativePath), PathComparison);
        }

        /// <summary>
        /// Marks one external identity path complete after its metadata has been repaired.
        /// </summary>
        internal void MarkMetadataPresent(string fullPath) {
            EnsureNotDisposed();
            if (!string.IsNullOrWhiteSpace(fullPath)) {
                MissingMetadataPaths.Remove(Path.GetFullPath(fullPath));
            }
        }

        /// <summary>
        /// Retains one path in the current missing-metadata snapshot after replaying its generated document.
        /// </summary>
        /// <param name="fullPath">Absolute authored asset path.</param>
        internal void MarkMetadataMissing(string fullPath) {
            EnsureNotDisposed();
            if (!string.IsNullOrWhiteSpace(fullPath)) {
                MissingMetadataPaths.Add(Path.GetFullPath(fullPath));
            }
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
            bool metadataCreated;
            AssetIdentityMetadataDocument document = LoadIdentityMetadata(normalizedFullPath, out metadataCreated);
            if (metadataCreated) {
                RepairReport.Append(new EditorAssetRepairRecord(
                    EditorAssetRepairKind.MissingExternalMetadataCreation,
                    relativePath,
                    string.Empty,
                    document.AssetId,
                    null,
                    "external identity document was missing",
                    normalizedFullPath + ".hmeta",
                    "Created missing external asset identity metadata."));
            }
            EditorAssetIdentityEntry entry = CreateEntry(normalizedFullPath, document);
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
        AssetIdentityMetadataDocument LoadIdentityMetadataForReconciliation(string fullPath, out bool created) {
            if (PathClassifier.UsesEmbeddedIdentity(fullPath)) {
                created = false;
                return MetadataService.Load(fullPath);
            }

            created = !File.Exists(fullPath + ".hmeta");
            return created
                ? new AssetIdentityMetadataDocument { AssetId = Guid.NewGuid().ToString("N") }
                : MetadataService.Load(fullPath);
        }

        AssetIdentityMetadataDocument LoadIdentityMetadata(string fullPath, out bool created) {
            if (PathClassifier.UsesEmbeddedIdentity(fullPath)) {
                created = false;
                return MetadataService.Load(fullPath);
            }

            created = !File.Exists(fullPath + ".hmeta");
            return MetadataService.LoadOrCreate(fullPath, string.Empty);
        }

        /// <summary>
        /// Applies all staged identity mutations after their exact paths have been published.
        /// </summary>
        void ApplyRepairBatch(IReadOnlyList<PendingIdentityRepair> pendingRepairs) {
            if (pendingRepairs == null || pendingRepairs.Count == 0) {
                return;
            }

            for (int index = 0; index < pendingRepairs.Count; index++) {
                pendingRepairs[index].CaptureOriginalState();
            }
            for (int index = 0; index < pendingRepairs.Count; index++) {
                ChangeLog.PublishChange(pendingRepairs[index].RelativePath);
            }

            int appliedCount = 0;
            try {
                for (int index = 0; index < pendingRepairs.Count; index++) {
                    RepairMutationHook?.Invoke(index);
                    MetadataService.Save(pendingRepairs[index].FullPath, pendingRepairs[index].Document);
                    HashCache.InvalidateContentHash(pendingRepairs[index].FullPath);
                    appliedCount++;
                }
            } catch {
                for (int index = appliedCount - 1; index >= 0; index--) {
                    pendingRepairs[index].RestoreOriginalState();
                }
                throw;
            }

            for (int index = 0; index < pendingRepairs.Count; index++) {
                RepairReport.Append(pendingRepairs[index].Report);
            }
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
        EditorAssetIdentityEntry SelectOwner(string assetId, IReadOnlyList<EditorAssetIdentityEntry> candidates, out bool selectedByRecordedOwner) {
            selectedByRecordedOwner = false;
            string previousOwnerPath;
            if (PreviousOwners.TryGetValue(assetId, out previousOwnerPath)) {
                for (int index = 0; index < candidates.Count; index++) {
                    if (string.Equals(candidates[index].RelativePath, previousOwnerPath, PathComparison)) {
                        selectedByRecordedOwner = true;
                        return candidates[index];
                    }
                }
            }
            return candidates.OrderBy(entry => entry.RelativePath, PathComparer).First();
        }

        /// <summary>
        /// Copies one validated identity document before a staged repair mutates it.
        /// </summary>
        static AssetIdentityMetadataDocument CloneDocument(AssetIdentityMetadataDocument document) {
            return new AssetIdentityMetadataDocument {
                Version = document.Version,
                AssetId = document.AssetId,
                FormerAssetIds = new List<string>(document.FormerAssetIds ?? new List<string>())
            };
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
        /// Holds one staged identity mutation and its exact pre-repair bytes.
        /// </summary>
        sealed class PendingIdentityRepair {
            readonly bool UsesEmbeddedIdentity;
            byte[] OriginalBytes;
            bool OriginalMetadataExists;
            byte[] OriginalMetadataBytes;

            public PendingIdentityRepair(string assetsRootPath, string fullPath, AssetIdentityMetadataDocument document, EditorAssetRepairRecord report) {
                FullPath = fullPath;
                Document = document;
                Report = report;
                RelativePath = NormalizeRelativePath(Path.GetRelativePath(assetsRootPath, fullPath));
                UsesEmbeddedIdentity = new EditorAssetPathClassifier().UsesEmbeddedIdentity(fullPath);
            }

            public string FullPath { get; }

            public string RelativePath { get; }

            public AssetIdentityMetadataDocument Document { get; }

            public EditorAssetRepairRecord Report { get; }

            public void CaptureOriginalState() {
                string metadataPath = UsesEmbeddedIdentity ? FullPath : FullPath + ".hmeta";
                if (UsesEmbeddedIdentity) {
                    OriginalBytes = File.ReadAllBytes(FullPath);
                } else {
                    OriginalMetadataExists = File.Exists(metadataPath);
                    OriginalMetadataBytes = OriginalMetadataExists ? File.ReadAllBytes(metadataPath) : null;
                }
            }

            public void RestoreOriginalState() {
                string metadataPath = UsesEmbeddedIdentity ? FullPath : FullPath + ".hmeta";
                if (UsesEmbeddedIdentity) {
                    File.WriteAllBytes(FullPath, OriginalBytes);
                    return;
                }

                if (OriginalMetadataExists) {
                    File.WriteAllBytes(metadataPath, OriginalMetadataBytes);
                } else if (File.Exists(metadataPath)) {
                    File.Delete(metadataPath);
                }
            }
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
