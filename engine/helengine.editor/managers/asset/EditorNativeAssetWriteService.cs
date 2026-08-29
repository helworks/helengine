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
        /// Session-owned material document builder used before transaction staging.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialAssetSettingsServiceValue;

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
            MetadataService = new AssetIdentityMetadataService(ProjectRootPath);
            MaterialAssetSettingsServiceValue = new MaterialAssetSettingsService(ProjectRootPath);
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
            MetadataService = new AssetIdentityMetadataService(ProjectRootPath);
            MaterialAssetSettingsServiceValue = new MaterialAssetSettingsService(ProjectRootPath);
            InitializeObservedState();
        }

        /// <summary>
        /// Prepares the common material document and all platform override
        /// documents without publishing any of them.
        /// </summary>
        internal EditorPreparedMaterialWrite PrepareMaterial(
            string relativePath,
            GeneratedMaterialAssetDefinition definition) {
            EnsureNotDisposed();
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (definition.MaterialAsset == null) {
                throw new InvalidOperationException("Generated material definitions must include a material asset.");
            }

            ResolveDestination(relativePath, out string normalizedRelativePath);
            string fullPath = Path.Combine(AssetsRootPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ValidateNoReparseTraversal(fullPath);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            ReconcileIfGenerationChanged();

            string authoringAssetId;
            IReadOnlyList<string> formerAssetIds;
            if (File.Exists(fullPath)) {
                ValidateMaterialSettingsPayload(EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath), fullPath, true);
                AssetIdentityMetadataDocument identity = MetadataService.Load(fullPath);
                authoringAssetId = identity.AssetId;
                formerAssetIds = identity.FormerAssetIds.ToArray();
                definition.MaterialAsset.AuthoringAssetId = authoringAssetId;
                definition.MaterialAsset.FormerAuthoringAssetIds = formerAssetIds.ToArray();
            } else {
                authoringAssetId = definition.MaterialAsset.AuthoringAssetId;
                if (!IsValidAssetId(authoringAssetId) || IdentityIndex.IsCurrentAssetIdOwned(authoringAssetId)) {
                    do {
                        authoringAssetId = Guid.NewGuid().ToString("N");
                    } while (IdentityIndex.IsCurrentAssetIdOwned(authoringAssetId));
                }
                definition.MaterialAsset.AuthoringAssetId = authoringAssetId;
                definition.MaterialAsset.FormerAuthoringAssetIds = Array.Empty<string>();
                formerAssetIds = Array.Empty<string>();
            }

            EditorGeneratedMaterialSettingsPayload payload = MaterialAssetSettingsServiceValue.BuildGeneratedPayload(
                definition,
                authoringAssetId,
                formerAssetIds);
            EditorPreparedAssetWrite common = PrepareRawPayload(
                normalizedRelativePath,
                payload.CommonBytes,
                authoringAssetId,
                "MaterialAssetCommonSettingsDocument",
                true);
            List<EditorPreparedAssetWrite> overrides = new List<EditorPreparedAssetWrite>();
            foreach (KeyValuePair<string, byte[]> entry in payload.OverrideBytesBySuffix) {
                overrides.Add(PrepareRawPayload(
                    normalizedRelativePath + entry.Key,
                    entry.Value,
                    string.Empty,
                    "MaterialAssetPlatformOverrideDocument",
                    false));
            }

            return new EditorPreparedMaterialWrite {
                Common = common,
                Overrides = overrides
            };
        }

        /// <summary>
        /// Prepares one serialized editor material-settings payload without
        /// touching the destination tree.
        /// </summary>
        internal EditorPreparedAssetWrite PrepareRawPayload(
            string relativePath,
            byte[] serializedBytes,
            string assetId,
            string assetKind,
            bool updatesIdentityIndex) {
            EnsureNotDisposed();
            if (serializedBytes == null || serializedBytes.Length == 0) {
                throw new ArgumentException("Serialized payload must not be empty.", nameof(serializedBytes));
            }
            ResolveDestination(relativePath, out string normalizedRelativePath);
            string fullPath = Path.Combine(AssetsRootPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ValidateNoReparseTraversal(fullPath);
            bool priorExists = File.Exists(fullPath);
            byte[] priorBytes = priorExists
                ? EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath)
                : null;
            if (updatesIdentityIndex && priorExists) {
                AssetIdentityMetadataDocument priorIdentity = MetadataService.Load(fullPath);
                assetId = priorIdentity.AssetId;
            }
            string serializedHash = HashCache.ComputeSerializedHash(serializedBytes);
            bool commonMaterialDocument = string.Equals(
                assetKind,
                "MaterialAssetCommonSettingsDocument",
                StringComparison.Ordinal);
            string contentHash = commonMaterialDocument
                ? ComputeCanonicalMaterialSettingsHash(serializedBytes)
                : serializedHash;
            string priorContentHash = priorExists
                ? (commonMaterialDocument
                    ? ComputeCanonicalMaterialSettingsHash(priorBytes)
                    : HashCache.ComputeSerializedHash(priorBytes))
                : null;
            EditorAuthoringTransactionPayloadKind payloadKind = commonMaterialDocument
                ? EditorAuthoringTransactionPayloadKind.MaterialCommonSettings
                : EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride;
            return new EditorPreparedAssetWrite {
                RelativePath = normalizedRelativePath,
                FullPath = fullPath,
                SerializedBytes = serializedBytes,
                ContentHash = contentHash,
                SerializedHash = serializedHash,
                AssetId = assetId ?? string.Empty,
                AssetKind = assetKind,
                PriorExists = priorExists,
                PriorIdentityMetadataExists = updatesIdentityIndex,
                PriorContentHash = priorContentHash,
                PriorSerializedHash = priorExists ? HashCache.ComputeSerializedHash(priorBytes) : null,
                PreservedExistingIdentity = updatesIdentityIndex && priorExists,
                IsUnchanged = priorExists && priorBytes.AsSpan().SequenceEqual(serializedBytes),
                IsMaterialSettingsPayload = true,
                PayloadKind = payloadKind,
                UpdatesIdentityIndex = updatesIdentityIndex
            };
        }

        /// <summary>
        /// Prepares identity-less generated bytes at a project-relative path.
        /// The supplied prior hash is mandatory for an existing destination;
        /// null explicitly means that the destination must not exist.
        /// </summary>
        internal EditorPreparedAssetWrite PrepareGeneratedFile(
            string projectRelativePath,
            byte[] bytes,
            string expectedPriorContentHash,
            EditorGeneratedFileKind fileKind) {
            return PrepareGeneratedFile(projectRelativePath, bytes, expectedPriorContentHash, fileKind, null);
        }

        /// <summary>
        /// Prepares generated source bytes with the external identity that is
        /// staged alongside the source by the owning session.
        /// </summary>
        internal EditorPreparedAssetWrite PrepareGeneratedFile(
            string projectRelativePath,
            byte[] bytes,
            string expectedPriorContentHash,
            EditorGeneratedFileKind fileKind,
            string externalAssetId) {
            EnsureNotDisposed();
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (string.IsNullOrWhiteSpace(projectRelativePath) || Path.IsPathRooted(projectRelativePath)) {
                throw new ArgumentException("Generated file path must be project-relative.", nameof(projectRelativePath));
            }

            string normalizedRelativePath = NormalizeProjectRelativePath(projectRelativePath);
            string fullPath = Path.Combine(ProjectRootPath, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            ValidateProjectRootTraversal(fullPath);
            bool priorExists = File.Exists(fullPath);
            byte[] priorBytes = priorExists
                ? EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath)
                : null;
            string priorHash = priorExists
                ? ComputeRawBytesHash(priorBytes)
                : null;
            if (priorExists != !string.IsNullOrWhiteSpace(expectedPriorContentHash)) {
                throw new IOException($"Generated file destination '{normalizedRelativePath}' does not match the expected prior-existence contract.");
            }
            if (priorExists && !string.Equals(priorHash, expectedPriorContentHash, StringComparison.Ordinal)) {
                throw new IOException($"Generated file destination '{normalizedRelativePath}' changed after its prior hash was captured.");
            }

            string stagedHash = ComputeRawBytesHash(bytes);
            string changeLogRelativePath = null;
            if (!string.IsNullOrWhiteSpace(externalAssetId) && normalizedRelativePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) {
                changeLogRelativePath = normalizedRelativePath.Substring("assets/".Length);
            }
            // A source with no identity sidecar still needs one publication
            // pass even when its bytes are unchanged. This makes the source
            // entry visible to the identity graph after the session stages
            // the companion .hmeta file.
            bool identityMetadataMissing = !string.IsNullOrWhiteSpace(externalAssetId)
                && !File.Exists(fullPath + ".hmeta");
            return new EditorPreparedAssetWrite {
                RelativePath = normalizedRelativePath,
                ChangeLogRelativePath = changeLogRelativePath,
                FullPath = fullPath,
                SerializedBytes = bytes.ToArray(),
                ContentHash = stagedHash,
                SerializedHash = stagedHash,
                AssetKind = fileKind.ToString(),
                PayloadKind = EditorAuthoringTransactionPayloadKind.GeneratedFile,
                UsesProjectRoot = true,
                PriorExists = priorExists,
                PriorIdentityMetadataExists = !identityMetadataMissing,
                PriorContentHash = priorHash,
                PriorSerializedHash = priorHash,
                IsUnchanged = priorExists && priorBytes.AsSpan().SequenceEqual(bytes) && !identityMetadataMissing,
                UpdatesIdentityIndex = fileKind == EditorGeneratedFileKind.Source && !string.IsNullOrWhiteSpace(externalAssetId),
                AssetId = externalAssetId ?? string.Empty,
                PreservedExistingIdentity = !string.IsNullOrWhiteSpace(externalAssetId) && priorExists
            };
        }

        /// <summary>
        /// Reads the current external identity for a source, or allocates the
        /// identity that the caller will persist in the staged metadata pair.
        /// </summary>
        internal string ResolveGeneratedSourceAssetId(string sourcePath) {
            EnsureNotDisposed();
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            string normalizedPath = Path.GetFullPath(sourcePath);
            ValidateNoReparseTraversal(normalizedPath);
            string metadataPath = normalizedPath + ".hmeta";
            if (File.Exists(metadataPath)) {
                if (!File.Exists(normalizedPath)) {
                    throw new InvalidOperationException($"Generated source identity metadata '{metadataPath}' has no source destination.");
                }
                return MetadataService.Load(normalizedPath).AssetId;
            }

            string assetId;
            do {
                assetId = Guid.NewGuid().ToString("N");
            } while (IdentityIndex.IsCurrentAssetIdOwned(assetId));
            return assetId;
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
            if (destinationExists && EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath).AsSpan().SequenceEqual(serializedBytes)) {
                disposition = EditorAssetWriteDisposition.Unchanged;
            } else {
                long publishedGeneration = ChangeLog.PublishChange(normalizedRelativePath);
                WriteAtomically(fullPath, serializedBytes, AssetsRootPath, ProjectRootPath);
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
            byte[] priorBytes = priorExists ? EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath) : null;
            bool unchanged = priorExists && priorBytes.AsSpan().SequenceEqual(serializedBytes);
            string priorContentHash = priorExists ? HashCache.ComputeContentHashFresh(fullPath) : null;
            // Compute the recovery hash from the exact serialized bytes that
            // will be journaled.  Re-serializing the live object and then
            // deserializing it during commit can normalize optional blueprint
            // fields, so hashing the object graph here would make a valid
            // staged payload fail its own integrity check.
            string stagedContentHash = ComputeCanonicalNativeHash(serializedBytes, fullPath);
            return new EditorPreparedAssetWrite {
                RelativePath = normalizedRelativePath,
                FullPath = fullPath,
                SerializedBytes = serializedBytes,
                ContentHash = stagedContentHash,
                SerializedHash = HashCache.ComputeSerializedHash(serializedBytes),
                AssetId = asset.AuthoringAssetId,
                AssetKind = GetExpectedValueKind(asset).ToString(),
                PriorExists = priorExists,
                PriorIdentityMetadataExists = true,
                PriorContentHash = priorContentHash,
                PriorSerializedHash = priorExists ? HashCache.ComputeSerializedHash(priorBytes) : null,
                PreservedExistingIdentity = preservedExistingIdentity,
                IsUnchanged = unchanged,
                PayloadKind = EditorAuthoringTransactionPayloadKind.NativeAsset,
                UpdatesIdentityIndex = true
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

            // Identity-less generated files (source, import-settings, and
            // cache payloads) are project-root outputs, not assets managed by
            // the native hash/index graph. Touching that graph for them would
            // reject valid first-time cache files during publication.
            if (!prepared.UsesProjectRoot) {
                HashCache.InvalidateContentHash(prepared.FullPath);
            }
            if (prepared.UpdatesIdentityIndex) {
                IdentityIndex.RegisterOrUpdateUnderLock(prepared.FullPath);
            }
            if (prepared.UpdatesIdentityIndex) {
                HashCache.GetContentHash(prepared.FullPath);
            }
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
                if (!prepared.UpdatesIdentityIndex) {
                    continue;
                }
                // Native identity-index paths are assets-relative, while
                // generated external sources are staged with their
                // project-relative `assets/` destination. Normalize the
                // comparison path before checking the claim so an existing
                // source is recognized as its own replacement.
                string identityRelativePath = prepared.RelativePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                    ? prepared.RelativePath.Substring("assets/".Length)
                    : prepared.RelativePath;
                if (string.IsNullOrWhiteSpace(prepared.AssetId) || !claims.Add(prepared.AssetId) ||
                    IdentityIndex.IsAssetIdentityClaimedByOtherPathUnderLock(prepared.AssetId, identityRelativePath)) {
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
        /// Publishes a transaction's restored paths through the idempotent
        /// rollback generation boundary.
        /// </summary>
        internal long PublishRollbackChangesUnderLock(string transactionId, IReadOnlyList<string> relativePaths) {
            EnsureNotDisposed();
            return ChangeLog.PublishRollbackChanges(transactionId, relativePaths);
        }

        /// <summary>
        /// Retires a completed rollback token without advancing the observed
        /// path generation.
        /// </summary>
        internal long PruneRollbackChangesUnderLock(string transactionId) {
            EnsureNotDisposed();
            return ChangeLog.PruneRollbackChanges(transactionId);
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
        /// Validates one staged material-settings payload against its durable
        /// transaction claims. Platform override documents intentionally have
        /// no identity claim of their own.
        /// </summary>
        internal void ValidatePreparedMaterialPayload(
            byte[] serializedBytes,
            string expectedContentHash,
            string expectedSerializedHash,
            string expectedAssetId,
            string expectedAssetKind) {
            bool commonDocument = string.Equals(expectedAssetKind, "MaterialAssetCommonSettingsDocument", StringComparison.Ordinal);
            ValidateMaterialSettingsPayload(serializedBytes, null, commonDocument);
            string actualSerializedHash = ComputeRawBytesHash(serializedBytes);
            if (!string.Equals(actualSerializedHash, expectedSerializedHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The staged material-settings payload does not match its journal.");
            }

            string actualContentHash = commonDocument
                ? ComputeCanonicalMaterialSettingsHash(serializedBytes)
                : actualSerializedHash;
            if (!string.Equals(actualContentHash, expectedContentHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The staged material-settings content hash does not match its journal.");
            }

            if (commonDocument) {
                using MemoryStream stream = new MemoryStream(serializedBytes, writable: false);
                MaterialAssetCommonSettingsDocument document = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream);
                if (!string.Equals(document.AuthoringAssetId, expectedAssetId, StringComparison.Ordinal)) {
                    throw new InvalidDataException("The staged material-settings identity does not match its journal.");
                }
            }
        }

        internal static void ValidateGeneratedFilePayload(
            byte[] serializedBytes,
            string expectedContentHash,
            string expectedAssetKind) {
            if (serializedBytes == null) {
                throw new InvalidDataException("The generated file payload is missing.");
            }
            if (!Enum.TryParse(expectedAssetKind, ignoreCase: false, out EditorGeneratedFileKind _)) {
                throw new InvalidDataException("The generated file payload kind is not recognized.");
            }
            if (!string.Equals(ComputeRawBytesHash(serializedBytes), expectedContentHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The generated file payload does not match its journal.");
            }
        }

        /// <summary>
        /// Validates the current material-settings binary container and, for a
        /// common document, its embedded identity.
        /// </summary>
        internal static void ValidateMaterialSettingsPayload(byte[] serializedBytes, string destinationPath, bool commonDocument) {
            if (serializedBytes == null || serializedBytes.Length == 0) {
                throw new InvalidDataException("The material-settings payload is empty.");
            }

            using MemoryStream stream = new MemoryStream(serializedBytes, writable: false);
            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ushort expectedValueKind = commonDocument
                ? (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument
                : (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetPlatformOverrideDocument;
            if (header.FormatId != global::helengine.files.EditorAssetBinarySerializer.FormatId ||
                header.RecordKind != (ushort)EditorBinaryRecordKind.AssetImportSettings ||
                header.ValueKind != expectedValueKind) {
                throw new InvalidDataException($"Material settings payload '{destinationPath}' is not a current material-settings document.");
            }
            stream.Position = 0;
            if (commonDocument) {
                MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream);
            } else {
                MaterialAssetPlatformOverrideDocumentBinarySerializer.Deserialize(stream);
            }
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

        /// <summary>
        /// Computes the material-settings content hash used by the live hash
        /// cache: embedded authoring identity and former identities are not
        /// part of the canonical content identity.
        /// </summary>
        internal static string ComputeCanonicalMaterialSettingsHash(byte[] serializedBytes) {
            ValidateMaterialSettingsPayload(serializedBytes, null, true);
            using MemoryStream input = new MemoryStream(serializedBytes, writable: false);
            MaterialAssetCommonSettingsDocument document = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(input);
            document.AuthoringAssetId = string.Empty;
            document.FormerAuthoringAssetIds.Clear();
            using MemoryStream canonical = new MemoryStream();
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(canonical, document);
            return ComputeRawBytesHash(canonical.ToArray());
        }

        internal static string ComputeRawBytesHash(byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            return string.Concat("sha256:", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
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
                throw new InvalidDataException($"The native payload identity or canonical hash does not match its journal for '{destinationPath}' (expected id '{expectedAssetId}', kind '{expectedAssetKind}', hash '{expectedContentHash}'; actual id '{asset.AuthoringAssetId}', kind '{GetExpectedValueKind(asset)}', hash '{ComputeCanonicalNativeHash(serializedBytes, destinationPath)}').");
            }
        }

        /// <summary>
        /// Restores one destination's in-memory identity graph after a failed publication.
        /// </summary>
        internal void RestorePublishedAssetUnderLock(EditorPreparedAssetWrite prepared) {
            EnsureNotDisposed();
            if (!prepared.UsesProjectRoot) {
                HashCache.InvalidateContentHash(prepared.FullPath);
            }
            if (prepared.PriorExists && prepared.UpdatesIdentityIndex) {
                if (prepared.PriorIdentityMetadataExists) {
                    IdentityIndex.RegisterOrUpdateUnderLock(prepared.FullPath);
                } else {
                    IdentityIndex.RemoveUnderLock(prepared.FullPath);
                }
            } else if (!prepared.PriorExists && prepared.UpdatesIdentityIndex) {
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

        static string NormalizeProjectRelativePath(string projectRelativePath) {
            string normalized = projectRelativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains("../", StringComparison.Ordinal)
                || normalized.Equals("..", StringComparison.Ordinal)) {
                throw new ArgumentException("Generated file path must be canonical and project-relative.", nameof(projectRelativePath));
            }
            return normalized;
        }

        void ValidateProjectRootTraversal(string fullPath) {
            string currentPath = Path.GetFullPath(fullPath);
            string rootPath = Path.GetFullPath(ProjectRootPath);
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Generated file destination '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }
                if (string.Equals(currentPath, rootPath, PathComparison)) {
                    return;
                }
                string parent = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrWhiteSpace(parent)
                    || (!string.Equals(parent, rootPath, PathComparison)
                        && !parent.StartsWith(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison))) {
                    throw new InvalidOperationException($"Generated file destination '{fullPath}' is outside the project root.");
                }
                currentPath = parent;
            }
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
                if (File.Exists(fullPath) && new EditorAssetPathClassifier(ProjectRootPath).IsAuthoredAsset(fullPath)) {
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
        void ValidateExistingNativeContainer(string fullPath, Asset asset) {
            using MemoryStream stream = new MemoryStream(EditorAuthoringMutationScope.ReadAllBytes(
                ProjectRootPath,
                fullPath), false);
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
        static void WriteAtomically(string fullPath, byte[] serializedBytes, string containingRoot, string projectRootPath) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Native asset destination does not include a writable directory.");
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directoryPath, containingRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, containingRoot);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(projectRootPath, fullPath, serializedBytes);
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
