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
        /// Initializes a project-scoped reference resolver.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="identityIndex">Optional identity index.</param>
        /// <param name="hashCache">Optional content hash cache.</param>
        /// <param name="metadataService">Optional identity metadata service.</param>
        /// <param name="pathClassifier">Optional path classifier.</param>
        public EditorAssetReferenceResolver(string projectRootPath, EditorAssetIdentityIndex identityIndex = null, EditorAssetHashCache hashCache = null, AssetIdentityMetadataService metadataService = null, EditorAssetPathClassifier pathClassifier = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            MetadataService = metadataService ?? new AssetIdentityMetadataService();
            PathClassifier = pathClassifier ?? new EditorAssetPathClassifier();
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
            IdentityIndex = identityIndex ?? new EditorAssetIdentityIndex(ProjectRootPath, MetadataService, PathClassifier, HashCache);
            IdentityIndex.Initialize();
        }

        /// <summary>
        /// Resolves one file-backed reference using UUID, path, then hash.
        /// </summary>
        /// <param name="reference">Saved file-backed reference.</param>
        /// <param name="expectedKind">Required asset category.</param>
        /// <returns>Resolved and canonicalized reference.</returns>
        public AssetReferenceResolution Resolve(SceneAssetReference reference, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
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
                string savedFullPath = Path.Combine(AssetsRootPath, savedPath.Replace('/', Path.DirectorySeparatorChar));
                pathMetadataWasMissing = IsMetadataMissing(savedFullPath);
            }

            bool metadataChanged = pathMetadataWasMissing;
            bool savedIdWasAdopted = false;
            if (pathMetadataWasMissing && IsValidAssetId(reference.AssetId) && !IdentityIndex.IsCurrentAssetIdOwned(reference.AssetId)) {
                string savedFullPath = Path.Combine(AssetsRootPath, savedPath.Replace('/', Path.DirectorySeparatorChar));
                AssetIdentityMetadataDocument document = MetadataService.Load(savedFullPath);
                document.AssetId = reference.AssetId;
                MetadataService.Save(savedFullPath, document);
                metadataChanged = true;
                savedIdWasAdopted = true;
                IdentityIndex.RegisterOrUpdate(savedFullPath);
            }

            EditorAssetIdentityEntry winner = SelectByAssetId(reference.AssetId, expectedKind, savedPath);
            AssetReferenceResolutionTier tier = AssetReferenceResolutionTier.AssetId;
            if (savedIdWasAdopted) {
                // The UUID was adopted from the saved path during this load, so report
                // the path tier that actually supplied the recovery information.
                winner = null;
                tier = AssetReferenceResolutionTier.Path;
            }
            if (winner == null) {
                EditorAssetIdentityEntry pathEntry = IdentityIndex.FindByPath(savedPath);
                if (pathEntry != null && pathEntry.EntryKind == expectedKind) {
                    winner = pathEntry;
                    tier = AssetReferenceResolutionTier.Path;
                }
            }
            if (winner == null && IsValidContentHash(reference.ContentHash)) {
                IReadOnlyList<EditorAssetIdentityEntry> candidates = IdentityIndex.EnumerateCompatible(expectedKind);
                for (int index = 0; index < candidates.Count; index++) {
                    if (string.Equals(HashCache.GetContentHash(candidates[index].FullPath), reference.ContentHash, StringComparison.Ordinal)) {
                        winner = candidates[index];
                        tier = AssetReferenceResolutionTier.ContentHash;
                        break;
                    }
                }
            }
            if (winner == null) {
                throw new InvalidOperationException($"Unable to resolve {expectedKind} asset reference. Tried AssetId='{reference.AssetId}', Path='{reference.RelativePath}', ContentHash='{reference.ContentHash}'.");
            }

            string contentHash = HashCache.GetContentHash(winner.FullPath);
            if (IsMetadataMissing(winner.FullPath)) {
                metadataChanged = true;
            }
            SceneAssetReference canonicalReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(winner.AssetId, winner.RelativePath, contentHash);
            bool referenceChanged = !AreEquivalent(reference, canonicalReference);
            return new AssetReferenceResolution(winner.FullPath, canonicalReference, tier, referenceChanged, metadataChanged);
        }

        /// <summary>Refreshes the identity index once for a multi-reference load or build scope.</summary>
        public void BeginResolutionScope() {
            EnsureNotDisposed();
            if (ResolutionScopeActive) {
                throw new InvalidOperationException("An asset reference resolution scope is already active.");
            }
            EnsureIdentityIndexInitialized();
            ResolutionScopeMissingMetadataPaths = IdentityIndex.CopyMissingMetadataPaths();
            ResolutionScopeActive = true;
        }

        /// <summary>Ends the active multi-reference resolution scope.</summary>
        public void EndResolutionScope() {
            EnsureNotDisposed();
            if (!ResolutionScopeActive) {
                throw new InvalidOperationException("No asset reference resolution scope is active.");
            }
            ResolutionScopeActive = false;
            ResolutionScopeMissingMetadataPaths = null;
        }

        /// <summary>
        /// Creates a canonical reference for an existing authored source file.
        /// </summary>
        /// <param name="fullPath">Absolute authored source path.</param>
        /// <param name="expectedKind">Required asset category.</param>
        /// <returns>Canonical stable reference.</returns>
        public SceneAssetReference CreateFileReference(string fullPath, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(fullPath));
            }
            string normalizedPath = Path.GetFullPath(fullPath);
            string assetsPrefix = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!normalizedPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Path '{fullPath}' must be inside the assets directory.");
            }
            if (!PathClassifier.IsAuthoredAsset(normalizedPath) || PathClassifier.Classify(normalizedPath) != expectedKind) {
                throw new InvalidOperationException($"Path '{fullPath}' is not an authored {expectedKind} asset.");
            }
            EnsureIdentityIndexInitialized();
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, normalizedPath));
            EditorAssetIdentityEntry entry = IdentityIndex.FindByPath(relativePath);
            entry ??= IdentityIndex.RegisterOrUpdate(normalizedPath);
            string contentHash = HashCache.GetContentHash(normalizedPath);
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(entry.AssetId, entry.RelativePath, contentHash);
        }

        /// <summary>Selects a UUID match, preferring the saved path among duplicate candidates.</summary>
        EditorAssetIdentityEntry SelectByAssetId(string assetId, AssetEntryKind expectedKind, string savedPath) {
            if (!IsValidAssetId(assetId)) {
                return null;
            }
            IReadOnlyList<EditorAssetIdentityEntry> candidates = IdentityIndex.FindByAssetId(assetId, expectedKind);
            for (int index = 0; index < candidates.Count; index++) {
                if (string.Equals(candidates[index].RelativePath, savedPath, StringComparison.Ordinal)) {
                    return candidates[index];
                }
            }
            return candidates.Count == 0 ? null : candidates[0];
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
            IsDisposed = true;
        }
    }
}
