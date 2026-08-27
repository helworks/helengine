namespace helengine.editor {
    /// <summary>
    /// Resolves authored editor references by stable UUID, path, and finally content hash.
    /// </summary>
    public sealed class EditorAssetReferenceResolver : IDisposable {
        readonly string ProjectRootPath;
        readonly string AssetsRootPath;
        readonly EditorAssetIdentityIndex IdentityIndex;
        readonly EditorAssetHashCache HashCache;
        readonly bool OwnsHashCache;
        readonly bool OwnsIdentityIndex;
        readonly AssetIdentityMetadataService MetadataService;
        readonly EditorAssetPathClassifier PathClassifier;
        readonly EditorAssetRepairReport RepairReport;
        IEditorAssetReadSynchronizer ReadSynchronizer;
        bool OwnsReadSynchronizer;
        bool ResolutionScopeActive;
        HashSet<string> ResolutionScopeMissingMetadataPaths;
        bool IsDisposed;

        /// <summary>
        /// Project root exposed only to other editor-owned services sharing this resolver.
        /// </summary>
        internal string ProjectRootPathValue {
            get {
                EnsureNotDisposed();
                return ProjectRootPath;
            }
        }

        /// <summary>
        /// Gets the identity index shared by this project-scoped resolver.
        /// </summary>
        internal EditorAssetIdentityIndex IdentityIndexValue {
            get {
                EnsureNotDisposed();
                return IdentityIndex;
            }
        }

        /// <summary>
        /// Gets the hash cache shared by this project-scoped resolver.
        /// </summary>
        internal EditorAssetHashCache HashCacheValue {
            get {
                EnsureNotDisposed();
                return HashCache;
            }
        }

        /// <summary>
        /// Gets the repair report shared by this resolver and its identity index.
        /// </summary>
        internal EditorAssetRepairReport RepairReportValue {
            get {
                EnsureNotDisposed();
                return RepairReport;
            }
        }

        /// <summary>
        /// Initializes a project-scoped reference resolver.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="identityIndex">Optional identity index.</param>
        /// <param name="hashCache">Optional content hash cache.</param>
        /// <param name="metadataService">Optional identity metadata service.</param>
        /// <param name="pathClassifier">Optional path classifier.</param>
        public EditorAssetReferenceResolver(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex = null,
            EditorAssetHashCache hashCache = null,
            AssetIdentityMetadataService metadataService = null,
            EditorAssetPathClassifier pathClassifier = null,
            EditorAssetRepairReport repairReport = null)
            : this(projectRootPath, identityIndex, hashCache, metadataService, pathClassifier, repairReport, null, true) {
        }

        /// <summary>
        /// Initializes a resolver with the project boundary already composed by its owning session.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="identityIndex">Session-owned identity index.</param>
        /// <param name="hashCache">Session-owned hash cache.</param>
        /// <param name="metadataService">Identity metadata service.</param>
        /// <param name="pathClassifier">Authored path classifier.</param>
        /// <param name="repairReport">Session-owned report.</param>
        /// <param name="readSynchronizer">Session-owned publication boundary.</param>
        internal EditorAssetReferenceResolver(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache,
            AssetIdentityMetadataService metadataService,
            EditorAssetPathClassifier pathClassifier,
            EditorAssetRepairReport repairReport,
            IEditorAssetReadSynchronizer readSynchronizer)
            : this(projectRootPath, identityIndex, hashCache, metadataService, pathClassifier, repairReport, readSynchronizer, false) {
        }

        EditorAssetReferenceResolver(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache,
            AssetIdentityMetadataService metadataService,
            EditorAssetPathClassifier pathClassifier,
            EditorAssetRepairReport repairReport,
            IEditorAssetReadSynchronizer readSynchronizer,
            bool createOwnedReadSynchronizer) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            MetadataService = metadataService ?? new AssetIdentityMetadataService();
            PathClassifier = pathClassifier ?? new EditorAssetPathClassifier();
            EditorAssetRepairReport indexedRepairReport = identityIndex?.RepairReportValue;
            if (indexedRepairReport != null && repairReport != null && !ReferenceEquals(indexedRepairReport, repairReport)) {
                throw new ArgumentException("An injected identity index and resolver must share one repair report.", nameof(repairReport));
            }
            RepairReport = indexedRepairReport ?? repairReport ?? new EditorAssetRepairReport();
            if (hashCache != null) {
                HashCache = hashCache;
                OwnsHashCache = false;
            } else if (identityIndex != null) {
                HashCache = identityIndex.HashCacheValue;
                OwnsHashCache = false;
            } else {
                HashCache = new EditorAssetHashCache(ProjectRootPath);
                OwnsHashCache = true;
            }
            OwnsIdentityIndex = identityIndex == null;
            IdentityIndex = identityIndex ?? new EditorAssetIdentityIndex(ProjectRootPath, MetadataService, PathClassifier, HashCache, RepairReport);
            IdentityIndex.Initialize();
            if (readSynchronizer != null) {
                ReadSynchronizer = readSynchronizer;
                OwnsReadSynchronizer = false;
            } else if (createOwnedReadSynchronizer) {
                ReadSynchronizer = new EditorNativeAssetWriteService(ProjectRootPath, IdentityIndex, HashCache);
                OwnsReadSynchronizer = true;
            } else {
                throw new ArgumentNullException(nameof(readSynchronizer));
            }
        }

        /// <summary>
        /// Resolves one file-backed reference using UUID, path, then hash.
        /// </summary>
        /// <param name="reference">Saved file-backed reference.</param>
        /// <param name="expectedKind">Required asset category.</param>
        /// <returns>Resolved and canonicalized reference.</returns>
        public AssetReferenceResolution Resolve(SceneAssetReference reference, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            return ExecuteSynchronizedRead(() => {
                if (reference == null) {
                    throw new ArgumentNullException(nameof(reference));
                }
                if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                    throw new InvalidOperationException("Only filesystem-backed asset references can be resolved by the editor resolver.");
                }

            EnsureIdentityIndexInitialized();
            bool pathMetadataWasMissing = false;
            string savedPath = NormalizeRelativePath(reference.RelativePath);
            if (!string.IsNullOrWhiteSpace(savedPath)) {
                string savedFullPath = ResolveInsideAssets(savedPath);
                pathMetadataWasMissing = IsMetadataMissing(savedFullPath);
            }

            bool metadataChanged = pathMetadataWasMissing;
            bool savedIdWasAdopted = false;
            if (pathMetadataWasMissing && IsValidAssetId(reference.AssetId) &&
                !IdentityIndex.IsAnyAssetIdentityClaimedUnderLock(reference.AssetId)) {
                string savedFullPath = ResolveInsideAssets(savedPath);
                AssetIdentityMetadataDocument document = MetadataService.Load(savedFullPath);
                string previousAssetId = document.AssetId;
                EditorAssetRepairRecord adoptionRepair = CreateRepairRecord(
                    EditorAssetRepairKind.SavedIdAdoption,
                    savedPath,
                    previousAssetId,
                    reference.AssetId,
                    AssetReferenceResolutionTier.Path,
                    "saved identity adopted by exact normalized path",
                "Adopted the saved identity for the existing authored source.");
                savedIdWasAdopted = IdentityIndex.TryAdoptSavedAssetIdUnderLock(savedFullPath, reference.AssetId, adoptionRepair);
                if (savedIdWasAdopted) {
                    metadataChanged = true;
                    IdentityIndex.MarkMetadataPresentUnderLock(savedFullPath);
                    if (ResolutionScopeActive) {
                        ResolutionScopeMissingMetadataPaths.Remove(savedFullPath);
                    }
                }
            }

            EditorAssetResolutionCandidateScore candidateEvidence = null;
            EditorAssetIdentityEntry winner = savedIdWasAdopted
                ? null
                : SelectByAssetId(reference.AssetId, expectedKind, savedPath, reference.ContentHash, out candidateEvidence);
            AssetReferenceResolutionTier tier = AssetReferenceResolutionTier.AssetId;
            if (savedIdWasAdopted) {
                // The UUID was adopted from the saved path during this load, so report
                // the path tier that actually supplied the recovery information.
                tier = AssetReferenceResolutionTier.Path;
            }
            if (winner == null) {
                EditorAssetIdentityEntry pathEntry = IdentityIndex.FindByPath(savedPath);
                if (pathEntry != null && pathEntry.EntryKind == expectedKind) {
                    winner = pathEntry;
                    tier = AssetReferenceResolutionTier.Path;
                    candidateEvidence = CreateCandidateScore(pathEntry, reference.AssetId, savedPath, false);
                }
            }
            if (winner == null && IsValidContentHash(reference.ContentHash)) {
                IReadOnlyList<EditorAssetIdentityEntry> candidates = IdentityIndex.EnumerateCompatible(expectedKind);
                List<EditorAssetIdentityEntry> hashMatches = new List<EditorAssetIdentityEntry>();
                for (int index = 0; index < candidates.Count; index++) {
                    ValidateNoReparseTraversal(candidates[index].FullPath);
                    if (string.Equals(HashCache.GetContentHash(candidates[index].FullPath), reference.ContentHash, StringComparison.Ordinal)) {
                        hashMatches.Add(candidates[index]);
                    }
                }
                winner = SelectBestCandidate(hashMatches, reference.AssetId, savedPath, reference.ContentHash, true, out candidateEvidence);
                if (winner != null) {
                    tier = AssetReferenceResolutionTier.ContentHash;
                }
            }
            if (winner == null) {
                throw new InvalidOperationException($"Unable to resolve {expectedKind} asset reference. Tried AssetId='{reference.AssetId}', Path='{reference.RelativePath}', ContentHash='{reference.ContentHash}'.");
            }

            ValidateNoReparseTraversal(winner.FullPath);
            string contentHash = HashCache.GetContentHash(winner.FullPath);
            if (IsMetadataMissing(winner.FullPath)) {
                metadataChanged = true;
            }
            SceneAssetReference canonicalReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(winner.AssetId, winner.RelativePath, contentHash);
            bool referenceChanged = !AreEquivalent(reference, canonicalReference);
            if (referenceChanged) {
                bool pathChanged = !string.Equals(reference.RelativePath, canonicalReference.RelativePath, StringComparison.Ordinal);
                bool hashChanged = !string.Equals(reference.ContentHash, canonicalReference.ContentHash, StringComparison.Ordinal);
                string evidence = candidateEvidence?.ToEvidenceString() ?? string.Empty;
                if (pathChanged) {
                    AppendRepair(EditorAssetRepairKind.PathHealing, winner.RelativePath, reference.AssetId, winner.AssetId, tier, evidence, "Healed the saved asset path to the selected authored source.");
                }
                if (hashChanged) {
                    AppendRepair(EditorAssetRepairKind.HashHealing, winner.RelativePath, reference.AssetId, winner.AssetId, tier, evidence, "Healed the saved content hash to the selected authored source.");
                }
                AppendRepair(EditorAssetRepairKind.CanonicalReferenceRefresh, winner.RelativePath, reference.AssetId, winner.AssetId, tier, evidence, "Refreshed the saved asset reference to its canonical identity, path, and hash.");
            }
                return new AssetReferenceResolution(winner.FullPath, canonicalReference, tier, referenceChanged, metadataChanged, candidateEvidence);
            });
        }

        /// <summary>Refreshes the identity index once for a multi-reference load or build scope.</summary>
        public void BeginResolutionScope() {
            EnsureNotDisposed();
            ExecuteSynchronizedRead(() => {
                if (ResolutionScopeActive) {
                    throw new InvalidOperationException("An asset reference resolution scope is already active.");
                }
                EnsureIdentityIndexInitialized();
                ResolutionScopeMissingMetadataPaths = IdentityIndex.CopyMissingMetadataPaths();
                ResolutionScopeActive = true;
                return true;
            });
        }

        /// <summary>Ends the active multi-reference resolution scope.</summary>
        public void EndResolutionScope() {
            EnsureNotDisposed();
            ExecuteSynchronizedRead(() => {
                if (!ResolutionScopeActive) {
                    throw new InvalidOperationException("No asset reference resolution scope is active.");
                }
                ResolutionScopeActive = false;
                ResolutionScopeMissingMetadataPaths = null;
                return true;
            });
        }

        /// <summary>
        /// Creates a canonical reference for an existing authored source file.
        /// </summary>
        /// <param name="fullPath">Absolute authored source path.</param>
        /// <param name="expectedKind">Required asset category.</param>
        /// <returns>Canonical stable reference.</returns>
        public SceneAssetReference CreateFileReference(string fullPath, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            return ExecuteSynchronizedRead(() => {
                if (string.IsNullOrWhiteSpace(fullPath)) {
                    throw new ArgumentException("Asset path must be provided.", nameof(fullPath));
                }
                string normalizedPath = ResolveInsideAssets(fullPath);
                if (!PathClassifier.IsAuthoredAsset(normalizedPath) || PathClassifier.Classify(normalizedPath) != expectedKind) {
                    throw new InvalidOperationException($"Path '{fullPath}' is not an authored {expectedKind} asset.");
                }
                EnsureIdentityIndexInitialized();
                string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, normalizedPath));
                EditorAssetIdentityEntry entry = IdentityIndex.FindByPath(relativePath);
                entry ??= IdentityIndex.RegisterOrUpdateUnderLock(normalizedPath);
                string contentHash = HashCache.GetContentHash(normalizedPath);
                return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(entry.AssetId, entry.RelativePath, contentHash);
            });
        }

        /// <summary>
        /// Attaches the one project publication boundary shared by this resolver and its owning session.
        /// </summary>
        internal void AttachReadSynchronizer(IEditorAssetReadSynchronizer synchronizer) {
            EnsureNotDisposed();
            if (synchronizer == null) {
                throw new ArgumentNullException(nameof(synchronizer));
            }
            if (ReadSynchronizer != null && !ReferenceEquals(ReadSynchronizer, synchronizer)) {
                if (!OwnsReadSynchronizer) {
                    throw new InvalidOperationException("An asset reference resolver can only use one project read boundary.");
                }

                (ReadSynchronizer as IDisposable)?.Dispose();
            }
            ReadSynchronizer = synchronizer;
            OwnsReadSynchronizer = false;
        }

        /// <summary>
        /// Executes a read through the owning publication boundary when one is attached.
        /// </summary>
        internal TResult ExecuteSynchronizedRead<TResult>(Func<TResult> read) {
            EnsureNotDisposed();
            if (read == null) {
                throw new ArgumentNullException(nameof(read));
            }
            // Every resolver is composed with a project publication boundary, either
            // borrowed from its session or owned by this standalone resolver.
            return ReadSynchronizer.Execute(read);
        }

        /// <summary>Selects a UUID match using the explicit deterministic candidate score.</summary>
        EditorAssetIdentityEntry SelectByAssetId(
            string assetId,
            AssetEntryKind expectedKind,
            string savedPath,
            string savedHash,
            out EditorAssetResolutionCandidateScore candidateEvidence) {
            candidateEvidence = null;
            if (!IsValidAssetId(assetId)) {
                return null;
            }
            IReadOnlyList<EditorAssetIdentityEntry> candidates = IdentityIndex.FindByAssetId(assetId, expectedKind);
            return SelectBestCandidate(candidates, assetId, savedPath, savedHash, false, out candidateEvidence);
        }

        /// <summary>Scores and selects one candidate in winner-first deterministic order.</summary>
        EditorAssetIdentityEntry SelectBestCandidate(
            IReadOnlyList<EditorAssetIdentityEntry> candidates,
            string savedAssetId,
            string savedPath,
            string savedHash,
            bool hashAlreadyMatched,
            out EditorAssetResolutionCandidateScore candidateEvidence) {
            candidateEvidence = null;
            if (candidates == null || candidates.Count == 0) {
                return null;
            }

            bool highestCurrentId = candidates.Any(candidate => string.Equals(candidate.AssetId, savedAssetId, StringComparison.Ordinal));
            bool highestTierHasPath = candidates.Any(candidate =>
                string.Equals(candidate.AssetId, savedAssetId, StringComparison.Ordinal) == highestCurrentId &&
                string.Equals(candidate.RelativePath, savedPath, PathComparison));
            bool evaluateHash = hashAlreadyMatched ||
                (!highestTierHasPath && IsValidContentHash(savedHash) && candidates.Count(candidate =>
                    string.Equals(candidate.AssetId, savedAssetId, StringComparison.Ordinal) == highestCurrentId) > 1);
            List<ScoredCandidate> scoredCandidates = new List<ScoredCandidate>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++) {
                EditorAssetIdentityEntry candidate = candidates[index];
                ValidateNoReparseTraversal(candidate.FullPath);
                bool matchesHash = hashAlreadyMatched;
                bool candidateIsHighestIdentityTier = string.Equals(candidate.AssetId, savedAssetId, StringComparison.Ordinal) == highestCurrentId;
                if (evaluateHash && !hashAlreadyMatched && candidateIsHighestIdentityTier) {
                    matchesHash = string.Equals(HashCache.GetContentHash(candidate.FullPath), savedHash, StringComparison.Ordinal);
                }
                EditorAssetResolutionCandidateScore score = CreateCandidateScore(candidate, savedAssetId, savedPath, matchesHash);
                scoredCandidates.Add(new ScoredCandidate(candidate, score));
            }

            scoredCandidates.Sort((left, right) => left.Score.CompareTo(right.Score));
            candidateEvidence = scoredCandidates[0].Score;
            return scoredCandidates[0].Entry;
        }

        /// <summary>Creates one immutable candidate score from indexed identity evidence.</summary>
        EditorAssetResolutionCandidateScore CreateCandidateScore(
            EditorAssetIdentityEntry candidate,
            string savedAssetId,
            string savedPath,
            bool matchesSavedHash) {
            return new EditorAssetResolutionCandidateScore(
                string.Equals(candidate.AssetId, savedAssetId, StringComparison.Ordinal),
                string.Equals(candidate.RelativePath, savedPath, PathComparison),
                matchesSavedHash,
                IdentityIndex.IsRecordedOwnerUnderLock(savedAssetId, candidate.RelativePath),
                candidate.RelativePath);
        }

        /// <summary>Records one automatic reference repair in the shared session report.</summary>
        void AppendRepair(
            EditorAssetRepairKind kind,
            string relativePath,
            string previousAssetId,
            string currentAssetId,
            AssetReferenceResolutionTier? tier,
            string evidence,
            string diagnostic) {
            RepairReport.Append(CreateRepairRecord(
                kind,
                relativePath,
                previousAssetId,
                currentAssetId,
                tier,
                evidence,
                diagnostic));
        }

        /// <summary>
        /// Creates one immutable repair record carrying the active binary document context.
        /// </summary>
        EditorAssetRepairRecord CreateRepairRecord(
            EditorAssetRepairKind kind,
            string relativePath,
            string previousAssetId,
            string currentAssetId,
            AssetReferenceResolutionTier? tier,
            string evidence,
            string diagnostic) {
            return new EditorAssetRepairRecord(
                kind,
                NormalizeRelativePath(relativePath),
                previousAssetId,
                currentAssetId,
                tier,
                evidence,
                NormalizeOwningDocument(EngineBinaryReadContext.CurrentAssetPath),
                diagnostic);
        }

        /// <summary>
        /// Normalizes the active binary document path for repair diagnostics.
        /// </summary>
        static string NormalizeOwningDocument(string documentPath) {
            return string.IsNullOrWhiteSpace(documentPath)
                ? string.Empty
                : Path.GetFullPath(documentPath);
        }

        /// <summary>Pairs one indexed entry with its immutable ordering score.</summary>
        sealed class ScoredCandidate {
            public ScoredCandidate(EditorAssetIdentityEntry entry, EditorAssetResolutionCandidateScore score) {
                Entry = entry;
                Score = score;
            }

            public EditorAssetIdentityEntry Entry { get; }

            public EditorAssetResolutionCandidateScore Score { get; }
        }

        /// <summary>Compares every persisted reference field.</summary>
        static bool AreEquivalent(SceneAssetReference left, SceneAssetReference right) {
            return left.SourceKind == right.SourceKind &&
                   string.Equals(left.RelativePath, right.RelativePath, StringComparison.Ordinal) &&
                   string.Equals(left.ProviderId, right.ProviderId, StringComparison.Ordinal) &&
                   string.Equals(left.AssetId, right.AssetId, StringComparison.Ordinal) &&
                   string.Equals(left.ContentHash, right.ContentHash, StringComparison.Ordinal);
        }

        /// <summary>Checks a lowercase separator-free UUID.</summary>
        static bool IsValidAssetId(string value) {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 32 && IsLowerHex(value);
        }

        /// <summary>Checks a lowercase SHA-256 content hash.</summary>
        static bool IsValidContentHash(string value) {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && IsLowerHex(value.Substring(7));
        }

        /// <summary>Checks lowercase hexadecimal text.</summary>
        static bool IsLowerHex(string value) {
            for (int index = 0; index < value.Length; index++) {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Normalizes an assets-relative path.</summary>
        static string NormalizeRelativePath(string value) {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Gets the operating-system path comparison used for saved-path winner selection.
        /// </summary>
        static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        /// <summary>
        /// Resolves a relative or absolute candidate and requires real containment beneath assets.
        /// </summary>
        string ResolveInsideAssets(string candidate) {
            if (string.IsNullOrWhiteSpace(candidate)) {
                throw new ArgumentException("Asset path must be provided.", nameof(candidate));
            }

            string normalizedPath = Path.IsPathRooted(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(Path.Combine(AssetsRootPath, candidate.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRoot = Path.GetFullPath(AssetsRootPath);
            string assetsPrefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!normalizedPath.StartsWith(assetsPrefix, comparison)) {
                throw new InvalidOperationException($"Path '{candidate}' must be inside the assets directory.");
            }

            ValidateNoReparseTraversal(normalizedPath);
            return normalizedPath;
        }

        /// <summary>Rejects links or junctions anywhere between assets and an authored candidate.</summary>
        void ValidateNoReparseTraversal(string fullPath) {
            string rootPath = Path.GetFullPath(AssetsRootPath);
            string currentPath = fullPath;
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Path '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }

                if (string.Equals(currentPath, rootPath, comparison)) {
                    return;
                }

                string parentPath = Path.GetDirectoryName(currentPath);
                string rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    (!string.Equals(parentPath, rootPath, comparison) && !parentPath.StartsWith(rootPrefix, comparison))) {
                    throw new InvalidOperationException($"Path '{fullPath}' must be inside the assets directory.");
                }
                currentPath = parentPath;
            }
        }

        /// <summary>
        /// Initializes the project identity index once for this resolver lifetime.
        /// </summary>
        void EnsureIdentityIndexInitialized() {
            IdentityIndex.Initialize();
        }

        /// <summary>
        /// Rejects operations after this resolver has released its owned resources.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorAssetReferenceResolver));
            }
        }

        /// <summary>
        /// Determines whether one authored path lacked external metadata at the current resolution boundary.
        /// </summary>
        /// <param name="fullPath">Absolute authored path.</param>
        /// <returns>True when metadata was absent during index initialization or scope capture.</returns>
        bool IsMetadataMissing(string fullPath) {
            string normalizedFullPath = Path.GetFullPath(fullPath);
            return ResolutionScopeActive
                ? ResolutionScopeMissingMetadataPaths.Contains(normalizedFullPath)
                : IdentityIndex.WasMetadataMissing(normalizedFullPath);
        }

        /// <summary>
        /// Releases a cache created by this resolver; borrowed caches remain owned by their caller.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            if (OwnsIdentityIndex) {
                IdentityIndex.Dispose();
            }
            if (OwnsHashCache) {
                HashCache.Dispose();
            }
            if (OwnsReadSynchronizer) {
                (ReadSynchronizer as IDisposable)?.Dispose();
            }
            IsDisposed = true;
        }
    }
}
