using System.Security.Cryptography;

namespace helengine.editor {
    /// <summary>
    /// Provides the project publication boundary used by readers of the shared identity graph.
    /// </summary>
    internal interface IEditorAssetReadSynchronizer {
        TResult Execute<TResult>(Func<TResult> read);
    }

    /// <summary>
    /// Writes current native asset payloads with stable embedded identity and byte-level idempotence.
    /// </summary>
    internal sealed class EditorNativeAssetWriteService : IEditorAssetReadSynchronizer, IDisposable {
        /// <summary>
        /// Canonical assets root owned by this writer.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Canonical assets root owned by this writer.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Session identity index updated after each successful write.
        /// </summary>
        readonly EditorAssetIdentityIndex IdentityIndex;

        /// <summary>
        /// Session hash cache used for the result recovery hash.
        /// </summary>
        readonly EditorAssetHashCache HashCache;

        /// <summary>
        /// Embedded identity reader for existing native destinations.
        /// </summary>
        readonly AssetIdentityMetadataService MetadataService;

        /// <summary>
        /// Last project publication generation observed by this writer.
        /// </summary>
        readonly IEditorProjectWriteChangeLog ChangeLog;

        /// <summary>
        /// Last project publication generation fully applied by this writer.
        /// </summary>
        long LastObservedGeneration;

        /// <summary>
        /// Tracks the current thread's ownership of this synchronizer boundary so nested resolver calls reuse the same lock.
        /// </summary>
        readonly ThreadLocal<bool> ReadBoundaryHeld = new ThreadLocal<bool>();

        bool IsDisposed;

        /// <summary>
        /// Initializes one native writer over the session-owned identity graph.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="identityIndex">Session-owned identity index.</param>
        /// <param name="hashCache">Session-owned hash cache.</param>
        public EditorNativeAssetWriteService(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            IdentityIndex = identityIndex ?? throw new ArgumentNullException(nameof(identityIndex));
            HashCache = hashCache ?? throw new ArgumentNullException(nameof(hashCache));
            ChangeLog = new FileEditorProjectWriteChangeLog(ProjectRootPath);
            MetadataService = new AssetIdentityMetadataService();
            InitializeObservedState();
        }

        /// <summary>
        /// Initializes one native writer with an instrumentable project change log.
        /// </summary>
        internal EditorNativeAssetWriteService(
            string projectRootPath,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache,
            IEditorProjectWriteChangeLog changeLog) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            IdentityIndex = identityIndex ?? throw new ArgumentNullException(nameof(identityIndex));
            HashCache = hashCache ?? throw new ArgumentNullException(nameof(hashCache));
            ChangeLog = changeLog ?? throw new ArgumentNullException(nameof(changeLog));
            MetadataService = new AssetIdentityMetadataService();
            InitializeObservedState();
        }

        /// <summary>
        /// Replays exact-path publications that may have occurred during index initialization.
        /// </summary>
        void InitializeObservedState() {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            LastObservedGeneration = 0;
            ReconcileIfGenerationChanged();
            long currentGeneration = ChangeLog.CurrentGeneration;
            if (currentGeneration > LastObservedGeneration) {
                LastObservedGeneration = currentGeneration;
            }
        }

        /// <summary>
        /// Runs a reference or hash read at the same publication boundary as writes.
        /// </summary>
        internal TResult ExecuteSynchronizedRead<TResult>(Func<TResult> read) {
            EnsureNotDisposed();
            if (read == null) {
                throw new ArgumentNullException(nameof(read));
            }

            if (ReadBoundaryHeld.Value) {
                return read();
            }

            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReadBoundaryHeld.Value = true;
            try {
                ReconcileIfGenerationChanged();
                return read();
            } finally {
                ReadBoundaryHeld.Value = false;
            }
        }

        /// <summary>
        /// Executes one shared identity read at the project publication boundary.
        /// </summary>
        TResult IEditorAssetReadSynchronizer.Execute<TResult>(Func<TResult> read) {
            return ExecuteSynchronizedRead(read);
        }

        /// <summary>
        /// Writes one current native asset beneath the canonical assets root.
        /// </summary>
        /// <param name="relativePath">Assets-relative native destination path.</param>
        /// <param name="asset">Native asset payload to serialize.</param>
        /// <returns>Disposition and canonical identity data for the destination.</returns>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            EnsureNotDisposed();
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string fullPath = ResolveDestination(relativePath, out string normalizedRelativePath);
            ValidateNativeDestination(fullPath, asset);
            ValidateNoReparseTraversal(fullPath);

            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReconcileIfGenerationChanged();
            ValidateNoReparseTraversal(fullPath);

            bool destinationExists = File.Exists(fullPath);
            bool preservedExistingIdentity = false;
            if (destinationExists) {
                ValidateExistingNativeContainer(fullPath, asset);
                CopyExistingIdentity(fullPath, asset);
                preservedExistingIdentity = true;
            } else {
                AssignNewDestinationIdentity(asset);
                asset.FormerAuthoringAssetIds = Array.Empty<string>();
            }

            byte[] serializedBytes = AssetSerializer.SerializeToBytes(asset);
            EditorAssetWriteDisposition disposition = destinationExists
                ? EditorAssetWriteDisposition.Changed
                : EditorAssetWriteDisposition.Created;
            if (destinationExists && File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(serializedBytes)) {
                disposition = EditorAssetWriteDisposition.Unchanged;
            } else {
                long publishedGeneration = ChangeLog.PublishChange(normalizedRelativePath);
                WriteAtomically(fullPath, serializedBytes, AssetsRootPath);
                HashCache.InvalidateContentHash(fullPath);
                IdentityIndex.RegisterOrUpdateUnderLock(fullPath);
                string replacedContentHash = HashCache.GetContentHash(fullPath);
                LastObservedGeneration = publishedGeneration;
                return new EditorAssetWriteResult(
                    normalizedRelativePath,
                    fullPath,
                    asset.AuthoringAssetId,
                    replacedContentHash,
                    disposition,
                    preservedExistingIdentity);
            }

            IdentityIndex.RegisterOrUpdateUnderLock(fullPath);
            string contentHash = HashCache.GetContentHash(fullPath);

            return new EditorAssetWriteResult(
                normalizedRelativePath,
                fullPath,
                asset.AuthoringAssetId,
                contentHash,
                disposition,
                preservedExistingIdentity);
        }

        /// <summary>
        /// Prepares canonical native bytes while leaving the assets tree untouched.
        /// </summary>
        internal EditorPreparedAssetWrite PrepareAsset(string relativePath, Asset asset) {
            EnsureNotDisposed();
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string fullPath = ResolveDestination(relativePath, out string normalizedRelativePath);
            ValidateNativeDestination(fullPath, asset);
            ValidateNoReparseTraversal(fullPath);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReconcileIfGenerationChanged();
            ValidateNoReparseTraversal(fullPath);

            bool priorExists = File.Exists(fullPath);
            bool preservedExistingIdentity = false;
            if (priorExists) {
                ValidateExistingNativeContainer(fullPath, asset);
                CopyExistingIdentity(fullPath, asset);
                preservedExistingIdentity = true;
            } else {
                AssignNewDestinationIdentity(asset);
                asset.FormerAuthoringAssetIds = Array.Empty<string>();
            }

            byte[] serializedBytes = AssetSerializer.SerializeToBytes(asset);
            byte[] priorBytes = priorExists ? File.ReadAllBytes(fullPath) : null;
            bool unchanged = priorExists && priorBytes.AsSpan().SequenceEqual(serializedBytes);
            string priorContentHash = priorExists ? HashCache.ComputeContentHashFresh(fullPath) : null;
            string stagedContentHash = HashCache.ComputeCanonicalAssetHash(asset);
            return new EditorPreparedAssetWrite {
                RelativePath = normalizedRelativePath,
                FullPath = fullPath,
                SerializedBytes = serializedBytes,
                ContentHash = stagedContentHash,
                SerializedHash = HashCache.ComputeSerializedHash(serializedBytes),
                AssetId = asset.AuthoringAssetId,
                AssetKind = GetExpectedValueKind(asset).ToString(),
                PriorExists = priorExists,
                PriorContentHash = priorContentHash,
                PriorSerializedHash = priorExists ? HashCache.ComputeSerializedHash(priorBytes) : null,
                PreservedExistingIdentity = preservedExistingIdentity,
                IsUnchanged = unchanged
            };
        }

        /// <summary>
        /// Applies one successfully replaced destination to the session-owned graph.
        /// </summary>
        internal void ApplyPublishedAssetUnderLock(EditorPreparedAssetWrite prepared) {
            EnsureNotDisposed();
            if (prepared == null) {
                throw new ArgumentNullException(nameof(prepared));
            }

            HashCache.InvalidateContentHash(prepared.FullPath);
            IdentityIndex.RegisterOrUpdateUnderLock(prepared.FullPath);
            HashCache.GetContentHash(prepared.FullPath);
        }

        /// <summary>
        /// Replays committed exact-path changes observed since this writer last
        /// synchronized, without performing a full assets enumeration.
        /// </summary>
        internal void ReconcileCommittedChangesUnderLock() {
            EnsureNotDisposed();
            ReconcileIfGenerationChanged();
        }

        /// <summary>
        /// Revalidates the complete transaction identity claim set after the
        /// latest committed generation has been replayed.
        /// </summary>
        internal void ValidatePreparedIdentityClaimsUnderLock(IReadOnlyList<EditorPreparedAssetWrite> preparedWrites) {
            EnsureNotDisposed();
            if (preparedWrites == null) {
                throw new ArgumentNullException(nameof(preparedWrites));
            }

            HashSet<string> claims = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < preparedWrites.Count; index++) {
                EditorPreparedAssetWrite prepared = preparedWrites[index] ?? throw new InvalidDataException("A transaction prepared output is missing.");
                if (string.IsNullOrWhiteSpace(prepared.AssetId) || !claims.Add(prepared.AssetId) ||
                    IdentityIndex.IsAssetIdentityClaimedByOtherPathUnderLock(prepared.AssetId, prepared.RelativePath)) {
                    throw new InvalidOperationException($"Native asset identity '{prepared.AssetId}' is claimed by another current transaction destination.");
                }
            }
        }

        /// <summary>
        /// Publishes one transaction destination generation while the project lock is held.
        /// </summary>
        internal long PublishChangeUnderLock(string relativePath) {
            EnsureNotDisposed();
            return ChangeLog.PublishChange(relativePath);
        }

        /// <summary>
        /// Publishes all changed transaction destinations as one generation snapshot.
        /// </summary>
        internal long PublishChangesUnderLock(IReadOnlyList<string> relativePaths) {
            EnsureNotDisposed();
            return ChangeLog.PublishChanges(relativePaths);
        }

        /// <summary>
        /// Updates this writer's observed publication generation after a transaction commit.
        /// </summary>
        internal void ObserveCurrentGenerationUnderLock() {
            EnsureNotDisposed();
            LastObservedGeneration = ChangeLog.CurrentGeneration;
        }

        /// <summary>
        /// Flushes the session-owned hash cache at a transaction commit boundary.
        /// </summary>
        internal void FlushHashCacheAtCommit() {
            EnsureNotDisposed();
            HashCache.Flush();
        }

        /// <summary>
        /// Computes one fresh recovery hash for a destination while bypassing the cache fingerprint.
        /// </summary>
        internal string ComputeCurrentContentHash(string fullPath) {
            EnsureNotDisposed();
            return HashCache.ComputeContentHashFresh(fullPath);
        }

        /// <summary>
        /// Computes a full serialized-byte fingerprint for transaction race checks.
        /// </summary>
        internal string ComputeSerializedHash(byte[] bytes) {
            EnsureNotDisposed();
            return HashCache.ComputeSerializedHash(bytes);
        }

        /// <summary>
        /// Validates that staged bytes are a current native payload of the destination kind.
        /// </summary>
        internal void ValidatePreparedPayload(
            byte[] serializedBytes,
            string destinationPath,
            string expectedContentHash,
            string expectedSerializedHash,
            string expectedAssetId,
            string expectedAssetKind) {
            EnsureNotDisposed();
            if (serializedBytes == null || serializedBytes.Length == 0) {
                throw new InvalidDataException("The staged native payload is empty.");
            }

            using MemoryStream stream = new MemoryStream(serializedBytes, writable: false);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            if (header.FormatId != global::helengine.files.EditorAssetBinarySerializer.FormatId ||
                header.RecordKind != (ushort)EditorBinaryRecordKind.Asset ||
                header.Version != global::helengine.files.EditorAssetBinarySerializer.CurrentVersion) {
                throw new InvalidDataException("The staged native payload is not a current asset document.");
            }

            ValidateNativePayloadIntegrity(serializedBytes, destinationPath, expectedContentHash, expectedSerializedHash, expectedAssetId, expectedAssetKind);
        }

        /// <summary>
        /// Validates one current native payload without requiring a session-owned cache.
        /// </summary>
        internal static void ValidateCurrentNativePayload(byte[] serializedBytes, string destinationPath) {
            if (serializedBytes == null || serializedBytes.Length == 0) {
                throw new InvalidDataException("The staged native payload is empty.");
            }

            using MemoryStream stream = new MemoryStream(serializedBytes, writable: false);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            if (header.FormatId != global::helengine.files.EditorAssetBinarySerializer.FormatId ||
                header.RecordKind != (ushort)EditorBinaryRecordKind.Asset ||
                header.Version != global::helengine.files.EditorAssetBinarySerializer.CurrentVersion) {
                throw new InvalidDataException("The staged native payload is not a current asset document.");
            }

            stream.Position = 0;
            Asset asset = AssetSerializer.Deserialize(stream);
            ValidateNativeDestination(destinationPath, asset);
        }

        /// <summary>
        /// Computes the canonical recovery hash of one current native payload.
        /// </summary>
        internal static string ComputeCanonicalNativeHash(byte[] serializedBytes, string destinationPath) {
            ValidateCurrentNativePayload(serializedBytes, destinationPath);
            using MemoryStream input = new MemoryStream(serializedBytes, writable: false);
            Asset asset = AssetSerializer.Deserialize(input);
            asset.AuthoringAssetId = string.Empty;
            asset.FormerAuthoringAssetIds = Array.Empty<string>();
            using MemoryStream canonical = new MemoryStream();
            AssetSerializer.Serialize(canonical, asset);
            return string.Concat("sha256:", Convert.ToHexString(SHA256.HashData(canonical.ToArray())).ToLowerInvariant());
        }

        internal static void ValidateNativePayloadIntegrity(
            byte[] serializedBytes,
            string destinationPath,
            string expectedContentHash,
            string expectedSerializedHash,
            string expectedAssetId,
            string expectedAssetKind) {
            ValidateCurrentNativePayload(serializedBytes, destinationPath);
            string actualSerializedHash = string.Concat("sha256:", Convert.ToHexString(SHA256.HashData(serializedBytes)).ToLowerInvariant());
            if (!string.Equals(actualSerializedHash, expectedSerializedHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The native payload serialized bytes do not match its journal.");
            }
            using MemoryStream input = new MemoryStream(serializedBytes, writable: false);
            Asset asset = AssetSerializer.Deserialize(input);
            if (!string.Equals(ComputeCanonicalNativeHash(serializedBytes, destinationPath), expectedContentHash, StringComparison.Ordinal) ||
                !string.Equals(asset.AuthoringAssetId, expectedAssetId, StringComparison.Ordinal) ||
                !string.Equals(GetExpectedValueKind(asset).ToString(), expectedAssetKind, StringComparison.Ordinal)) {
                throw new InvalidDataException("The native payload identity or canonical hash does not match its journal.");
            }
        }

        /// <summary>
        /// Restores one destination's in-memory identity graph after a failed publication.
        /// </summary>
        internal void RestorePublishedAssetUnderLock(EditorPreparedAssetWrite prepared) {
            EnsureNotDisposed();
            HashCache.InvalidateContentHash(prepared.FullPath);
            if (prepared.PriorExists) {
                IdentityIndex.RegisterOrUpdateUnderLock(prepared.FullPath);
            } else {
                IdentityIndex.RemoveUnderLock(prepared.FullPath);
            }
        }

        /// <summary>
        /// Releases the thread-local state owned by a standalone resolver boundary.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            ReadBoundaryHeld.Dispose();
            IsDisposed = true;
        }

        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorNativeAssetWriteService));
            }
        }

        /// <summary>
        /// Resolves and validates an assets-relative destination without touching the filesystem.
        /// </summary>
        /// <param name="relativePath">Candidate relative destination.</param>
        /// <param name="normalizedRelativePath">Canonical slash-separated relative path.</param>
        /// <returns>Canonical absolute destination.</returns>
        string ResolveDestination(string relativePath, out string normalizedRelativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            }
            if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must not be rooted.", nameof(relativePath));
            }

            string canonicalAssetsRoot = Path.GetFullPath(AssetsRootPath);
            string fullPath = Path.GetFullPath(Path.Combine(
                canonicalAssetsRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsPrefix = canonicalAssetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsPrefix, PathComparison)) {
                throw new InvalidOperationException("Native asset destinations must remain beneath the project assets root.");
            }
            if (string.Equals(fullPath, canonicalAssetsRoot, PathComparison)) {
                throw new InvalidOperationException("Native asset destinations must identify a file beneath the project assets root.");
            }

            normalizedRelativePath = Path.GetRelativePath(canonicalAssetsRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
            return fullPath;
        }

        /// <summary>
        /// Gets the operating-system path comparison used for containment checks.
        /// </summary>
        static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        /// <summary>
        /// Validates the destination extension and the asset type before any destination access.
        /// </summary>
        /// <param name="fullPath">Candidate absolute destination.</param>
        /// <param name="asset">Asset payload to serialize.</param>
        internal static void ValidateNativeDestination(string fullPath, Asset asset) {
            if (asset is ShaderAsset || asset is TextAsset || asset is PlatformMaterialAsset) {
                throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not a supported native authoring destination.");
            }

            string expectedExtension = asset switch {
                SceneAsset => SceneAsset.FileExtension,
                BlueprintAsset => BlueprintAsset.FileExtension,
                AnimationClipAsset => ".hanim",
                TextureAsset => EditorFileTemplateRegistry.MaterialExtension,
                ModelAsset => EditorFileTemplateRegistry.MaterialExtension,
                MaterialAsset => EditorFileTemplateRegistry.MaterialExtension,
                AudioAsset => EditorFileTemplateRegistry.MaterialExtension,
                _ => throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not a supported native authoring destination.")
            };
            if (!string.Equals(Path.GetExtension(fullPath), expectedExtension, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Native asset destination '{fullPath}' must use extension '{expectedExtension}' for asset type '{asset.GetType().Name}'.");
            }
        }

        /// <summary>
        /// Rejects reparse-point traversal before the writer touches destination state.
        /// </summary>
        /// <param name="fullPath">Candidate absolute destination.</param>
        void ValidateNoReparseTraversal(string fullPath) {
            string rootPath = Path.GetFullPath(AssetsRootPath);
            string currentPath = fullPath;
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Native asset destination '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }
                if (string.Equals(currentPath, rootPath, PathComparison)) {
                    break;
                }
                string parent = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrWhiteSpace(parent) ||
                    (!string.Equals(parent, rootPath, PathComparison) &&
                     !parent.StartsWith(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison))) {
                    throw new InvalidOperationException($"Native asset destination '{fullPath}' is outside the project assets root.");
                }
                currentPath = parent;
            }
        }

        /// <summary>
        /// Reconciles one publication generation observed from another authoring session.
        /// </summary>
        void ReconcileIfGenerationChanged() {
            IReadOnlyList<EditorProjectWriteChange> changes = ChangeLog.ReadAfter(LastObservedGeneration);
            if (changes.Count == 0) {
                return;
            }

            for (int index = 0; index < changes.Count; index++) {
                EditorProjectWriteChange change = changes[index];
                string fullPath = ResolveDestination(change.RelativePath, out _);
                ValidateNoReparseTraversal(fullPath);
                if (File.Exists(fullPath) && new EditorAssetPathClassifier().IsAuthoredAsset(fullPath)) {
                    bool metadataWasMissing = IdentityIndex.WasMetadataMissing(fullPath);
                    IdentityIndex.RegisterOrUpdateUnderLock(fullPath);
                    if (metadataWasMissing) {
                        IdentityIndex.MarkMetadataMissingUnderLock(fullPath);
                    }
                } else {
                    IdentityIndex.RemoveUnderLock(fullPath);
                }
                HashCache.InvalidateContentHash(fullPath);
                LastObservedGeneration = change.Generation;
            }
        }

        /// <summary>
        /// Validates that an existing destination is a current native asset of the requested type.
        /// </summary>
        /// <param name="fullPath">Existing destination path.</param>
        /// <param name="asset">Incoming asset payload.</param>
        static void ValidateExistingNativeContainer(string fullPath, Asset asset) {
            using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            EditorAssetBinaryValueKind expectedValueKind = GetExpectedValueKind(asset);
            if (header.FormatId != global::helengine.files.EditorAssetBinarySerializer.FormatId ||
                header.RecordKind != (ushort)EditorBinaryRecordKind.Asset ||
                header.Version != global::helengine.files.EditorAssetBinarySerializer.CurrentVersion ||
                header.ValueKind != (ushort)expectedValueKind) {
                throw new InvalidOperationException($"Native asset destination '{fullPath}' is not a current '{expectedValueKind}' payload.");
            }
        }

        /// <summary>
        /// Maps one accepted asset payload to its serialized value kind.
        /// </summary>
        /// <param name="asset">Asset payload to classify.</param>
        /// <returns>Expected current binary value kind.</returns>
        internal static EditorAssetBinaryValueKind GetExpectedValueKind(Asset asset) {
            if (asset is TextureAsset) return EditorAssetBinaryValueKind.TextureAsset;
            if (asset is ModelAsset) return EditorAssetBinaryValueKind.ModelAsset;
            if (asset is MaterialAsset) return EditorAssetBinaryValueKind.MaterialAsset;
            if (asset is AnimationClipAsset) return EditorAssetBinaryValueKind.AnimationClipAsset;
            if (asset is AudioAsset) return EditorAssetBinaryValueKind.AudioAsset;
            if (asset is SceneAsset) return EditorAssetBinaryValueKind.SceneAsset;
            if (asset is BlueprintAsset) return EditorAssetBinaryValueKind.BlueprintAsset;
            throw new InvalidOperationException($"Asset type '{asset.GetType().Name}' is not a supported native authoring destination.");
        }

        /// <summary>
        /// Copies identity from a valid current native destination into the new payload.
        /// </summary>
        /// <param name="fullPath">Existing native destination.</param>
        /// <param name="asset">Incoming payload receiving identity.</param>
        void CopyExistingIdentity(string fullPath, Asset asset) {
            AssetIdentityMetadataDocument identity = MetadataService.Load(fullPath);
            asset.AuthoringAssetId = identity.AssetId;
            asset.FormerAuthoringAssetIds = identity.FormerAssetIds.ToArray();
        }

        /// <summary>
        /// Accepts an unowned valid caller identity or mints a fresh current identity.
        /// </summary>
        /// <param name="asset">New payload receiving the destination identity.</param>
        void AssignNewDestinationIdentity(Asset asset) {
            if (IsValidAssetId(asset.AuthoringAssetId)) {
                if (IdentityIndex.IsCurrentAssetIdOwned(asset.AuthoringAssetId)) {
                    throw new InvalidOperationException($"Native asset identity '{asset.AuthoringAssetId}' is already owned by another current asset.");
                }
                return;
            }

            string assetId;
            do {
                assetId = Guid.NewGuid().ToString("N");
            } while (IdentityIndex.IsCurrentAssetIdOwned(assetId));
            asset.AuthoringAssetId = assetId;
        }

        /// <summary>
        /// Writes serialized bytes through a temporary file and one destination replacement.
        /// </summary>
        /// <param name="fullPath">Canonical destination path.</param>
        /// <param name="serializedBytes">Complete current-format bytes.</param>
        static void WriteAtomically(string fullPath, byte[] serializedBytes, string containingRoot) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Native asset destination does not include a writable directory.");
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directoryPath, containingRoot);
            Directory.CreateDirectory(directoryPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporaryPath, containingRoot);
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                    stream.Write(serializedBytes, 0, serializedBytes.Length);
                    stream.Flush(true);
                }
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
                File.Move(temporaryPath, fullPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporaryPath, containingRoot);
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Determines whether an identity is a valid lowercase 32-character hexadecimal value.
        /// </summary>
        /// <param name="assetId">Candidate identity.</param>
        /// <returns>True when the identity is valid.</returns>
        static bool IsValidAssetId(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 32) {
                return false;
            }
            for (int index = 0; index < assetId.Length; index++) {
                char character = assetId[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }
    }
}
