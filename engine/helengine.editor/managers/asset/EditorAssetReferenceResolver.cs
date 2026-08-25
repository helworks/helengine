namespace helengine.editor {
    /// <summary>
    /// Resolves authored editor references by stable UUID, path, and finally content hash.
    /// </summary>
    public sealed class EditorAssetReferenceResolver {
        readonly string ProjectRootPath;
        readonly string AssetsRootPath;
        readonly EditorAssetIdentityIndex IdentityIndex;
        readonly EditorAssetHashCache HashCache;
        readonly AssetIdentityMetadataService MetadataService;
        readonly EditorAssetPathClassifier PathClassifier;
        bool ResolutionScopeActive;
        HashSet<string> ResolutionScopeMissingMetadataPaths;

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
            HashCache = hashCache ?? new EditorAssetHashCache(ProjectRootPath);
            IdentityIndex = identityIndex ?? new EditorAssetIdentityIndex(ProjectRootPath, MetadataService, PathClassifier, HashCache);
        }

        /// <summary>
        /// Resolves one file-backed reference using UUID, path, then hash.
        /// </summary>
        /// <param name="reference">Saved file-backed reference.</param>
        /// <param name="expectedKind">Required asset category.</param>
        /// <returns>Resolved and canonicalized reference.</returns>
        public AssetReferenceResolution Resolve(SceneAssetReference reference, AssetEntryKind expectedKind) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                throw new InvalidOperationException("Only filesystem-backed asset references can be resolved by the editor resolver.");
            }

            bool pathMetadataWasMissing = false;
            HashSet<string> missingMetadataPaths = ResolutionScopeActive
                ? ResolutionScopeMissingMetadataPaths
                : FindMissingMetadataPaths();
            string savedPath = NormalizeRelativePath(reference.RelativePath);
            if (!string.IsNullOrWhiteSpace(savedPath)) {
                string savedFullPath = Path.Combine(AssetsRootPath, savedPath.Replace('/', Path.DirectorySeparatorChar));
                pathMetadataWasMissing = missingMetadataPaths.Contains(Path.GetFullPath(savedFullPath));
            }

            if (!ResolutionScopeActive) {
                IdentityIndex.Refresh();
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
                IdentityIndex.Refresh();
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
            if (missingMetadataPaths.Contains(winner.FullPath)) {
                metadataChanged = true;
            }
            SceneAssetReference canonicalReference = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(winner.AssetId, winner.RelativePath, contentHash);
            bool referenceChanged = !AreEquivalent(reference, canonicalReference);
            return new AssetReferenceResolution(winner.FullPath, canonicalReference, tier, referenceChanged, metadataChanged);
        }

        /// <summary>Refreshes the identity index once for a multi-reference load or build scope.</summary>
        public void BeginResolutionScope() {
            if (ResolutionScopeActive) {
                throw new InvalidOperationException("An asset reference resolution scope is already active.");
            }
            ResolutionScopeMissingMetadataPaths = FindMissingMetadataPaths();
            IdentityIndex.Refresh();
            ResolutionScopeActive = true;
        }

        /// <summary>Ends the active multi-reference resolution scope.</summary>
        public void EndResolutionScope() {
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
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(fullPath));
            }
            string normalizedPath = Path.GetFullPath(fullPath);
            if (!PathClassifier.IsAuthoredAsset(normalizedPath) || PathClassifier.Classify(normalizedPath) != expectedKind) {
                throw new InvalidOperationException($"Path '{fullPath}' is not an authored {expectedKind} asset.");
            }
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, normalizedPath));
            EditorAssetIdentityEntry entry = ResolutionScopeActive
                ? IdentityIndex.FindByPath(relativePath)
                : null;
            entry ??= IdentityIndex.RegisterOrRefresh(normalizedPath);
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

        /// <summary>Finds external authored sources whose identity sidecar is absent before index refresh.</summary>
        /// <returns>Absolute external authored paths without metadata sidecars.</returns>
        HashSet<string> FindMissingMetadataPaths() {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(AssetsRootPath)) {
                return paths;
            }
            foreach (string path in Directory.EnumerateFiles(AssetsRootPath, "*", SearchOption.AllDirectories)) {
                if (PathClassifier.IsAuthoredAsset(path) &&
                    !PathClassifier.UsesEmbeddedIdentity(path) &&
                    !File.Exists(path + ".hmeta")) {
                    paths.Add(Path.GetFullPath(path));
                }
            }
            return paths;
        }
    }
}
