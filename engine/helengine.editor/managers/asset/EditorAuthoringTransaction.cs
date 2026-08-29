using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Stages current native asset outputs and publishes them as one recoverable batch.
    /// </summary>
    public sealed class EditorAuthoringTransaction : IDisposable {
        readonly string ProjectRootPath;
        readonly string AssetsRootPath;
        string TransactionDirectoryPath;
        string ManifestPath;
        readonly EditorNativeAssetWriteService NativeWriter;
        readonly object OwnerSessionToken;
        readonly Action CompletionCallback;
        readonly EditorAuthoringTransactionHooks Hooks;
        readonly Dictionary<string, EditorPreparedAssetWrite> PreparedByPath;
        readonly object StateGate = new object();
        FileStream LeaseStream;
        EditorAuthoringVerifiedFile LeaseFile;
        EditorAuthoringMutationScope LeaseMutationScope;
        EditorAuthoringTransactionDocument Document;
        EditorAuthoringTransactionOutcome OutcomeValue = EditorAuthoringTransactionOutcome.Active;
        bool IsDisposed;
        bool RetiredDirectory;

        internal EditorAuthoringTransaction(
            string projectRootPath,
            EditorNativeAssetWriteService nativeWriter,
            Action completionCallback,
            object ownerSessionToken,
            EditorAuthoringTransactionHooks hooks = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            NativeWriter = nativeWriter ?? throw new ArgumentNullException(nameof(nativeWriter));
            CompletionCallback = completionCallback ?? throw new ArgumentNullException(nameof(completionCallback));
            OwnerSessionToken = ownerSessionToken ?? throw new ArgumentNullException(nameof(ownerSessionToken));
            Hooks = hooks ?? new EditorAuthoringTransactionHooks();
            PreparedByPath = new Dictionary<string, EditorPreparedAssetWrite>(PathComparer);

            string transactionRoot = EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(ProjectRootPath);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            using EditorAuthoringMutationScope mutationScope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                transactionRoot);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(transactionRoot, ProjectRootPath);
            EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, transactionRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(transactionRoot, ProjectRootPath);
            string transactionId = Guid.NewGuid().ToString("N");
            string creatingDirectoryPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                transactionRoot,
                ".creating-" + transactionId,
                "creating transaction");
            string publishedDirectoryPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                transactionRoot,
                transactionId,
                "transaction");
            TransactionDirectoryPath = creatingDirectoryPath;
            ManifestPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "transaction.json", "manifest");
            bool publishedDirectory = false;
            try {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(TransactionDirectoryPath, transactionRoot);
                EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, TransactionDirectoryPath);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(TransactionDirectoryPath, transactionRoot);
                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "staged", "staged-root");
                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "backups", "backup-root");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                    Path.Combine(TransactionDirectoryPath, "staged"),
                    TransactionDirectoryPath);
                EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, Path.Combine(TransactionDirectoryPath, "staged"));
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                    Path.Combine(TransactionDirectoryPath, "staged"),
                    TransactionDirectoryPath);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                    Path.Combine(TransactionDirectoryPath, "backups"),
                    TransactionDirectoryPath);
                EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, Path.Combine(TransactionDirectoryPath, "backups"));
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                    Path.Combine(TransactionDirectoryPath, "backups"),
                    TransactionDirectoryPath);
                string leasePath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "lease", "lease");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(leasePath, TransactionDirectoryPath);
                // Publish the directory only after a valid lease artifact and
                // manifest exist. Do not hold the exclusive lease handle across
                // the directory rename: Windows denies moving a directory that
                // contains an open non-shareable handle. Reacquire the handle
                // in the final directory while the project lock is still held.
                EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, leasePath, Array.Empty<byte>());
                Document = new EditorAuthoringTransactionDocument {
                    TransactionId = transactionId,
                    State = EditorAuthoringTransactionState.Staging
                };
                Hooks.BeforeManifestWrite?.Invoke();
                WriteDocument();
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(TransactionDirectoryPath, transactionRoot);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(publishedDirectoryPath, transactionRoot);
                EditorAuthoringMutationScope.MoveDirectory(ProjectRootPath, TransactionDirectoryPath, publishedDirectoryPath);
                publishedDirectory = true;
                TransactionDirectoryPath = publishedDirectoryPath;
                ManifestPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "transaction.json", "manifest");
                OpenLease();
            } catch (Exception primaryException) {
                try {
                    CloseLease();
                    string cleanupDirectory = publishedDirectory ? publishedDirectoryPath : creatingDirectoryPath;
                    if (Directory.Exists(cleanupDirectory)) {
                        EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(cleanupDirectory, transactionRoot);
                        if (publishedDirectory) {
                            string deletingDirectory = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                                transactionRoot,
                                ".deleting-" + transactionId,
                                "construction retirement");
                            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(deletingDirectory, transactionRoot);
                            EditorAuthoringMutationScope.MoveDirectory(ProjectRootPath, cleanupDirectory, deletingDirectory);
                            EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(deletingDirectory, transactionRoot);
                            EditorAuthoringMutationScope.DeleteDirectoryTree(ProjectRootPath, deletingDirectory, transactionRoot);
                        } else {
                            EditorAuthoringMutationScope.DeleteDirectoryTree(ProjectRootPath, cleanupDirectory, transactionRoot);
                        }
                    }
                } catch (Exception cleanupException) {
                    throw new AggregateException("Authoring transaction construction and cleanup failed.", primaryException, cleanupException);
                }
                throw;
            }
        }

        /// <summary>
        /// Gets the transaction identifier used by the staging directory.
        /// </summary>
        public string TransactionId => Document.TransactionId;

        /// <summary>
        /// Gets the canonical project root owned by this transaction.
        /// </summary>
        public string ProjectRootPathValue => ProjectRootPath;

        /// <summary>
        /// Gets the opaque session token that created this transaction.
        /// Consumers use reference identity to prevent same-root sessions from
        /// borrowing one another's publication transaction.
        /// </summary>
        internal object OwnerSessionTokenValue => OwnerSessionToken;

        /// <summary>
        /// Gets the current durable transaction state.
        /// </summary>
        public EditorAuthoringTransactionState State => Document.State;

        /// <summary>
        /// Gets the terminal outcome of this transaction, or Active while it
        /// remains available for staging/publication.
        /// </summary>
        public EditorAuthoringTransactionOutcome Outcome => OutcomeValue;

        /// <summary>
        /// Stages one canonical native asset without touching its destination.
        /// </summary>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            lock (StateGate) {
                EnsureStaging();
                if (asset == null) {
                    throw new ArgumentNullException(nameof(asset));
                }
                string normalizedHint = NormalizeRelativePath(relativePath);
                if (PreparedByPath.TryGetValue(normalizedHint, out EditorPreparedAssetWrite previous)) {
                    asset.AuthoringAssetId = previous.AssetId;
                    asset.FormerAuthoringAssetIds = Array.Empty<string>();
                }

                return StagePrepared(NativeWriter.PrepareAsset(relativePath, asset));
            }
        }

        /// <summary>
        /// Stages identity-less generated source, import-settings, or cache
        /// bytes at a project-relative path. A null prior hash explicitly
        /// means the destination must not exist; existing destinations must
        /// provide their exact prior byte hash.
        /// </summary>
        public EditorAssetWriteResult WriteGeneratedFile(
            string projectRelativePath,
            byte[] bytes,
            string expectedPriorContentHash,
            EditorGeneratedFileKind fileKind) {
            lock (StateGate) {
                EnsureStaging();
                return StagePrepared(NativeWriter.PrepareGeneratedFile(
                    projectRelativePath,
                    bytes,
                    expectedPriorContentHash,
                    fileKind));
            }
        }

        /// <summary>
        /// Reads output already staged by this transaction without consulting
        /// the live destination. Dependent generated values can therefore be
        /// materialized before publication.
        /// </summary>
        public byte[] ReadStagedFile(string projectRelativePath) {
            lock (StateGate) {
                EnsureStaging();
                string normalizedPath = NormalizeProjectRelativePath(projectRelativePath);
                if (!PreparedByPath.TryGetValue(normalizedPath, out EditorPreparedAssetWrite prepared)) {
                    throw new InvalidOperationException($"The transaction has not staged generated output '{normalizedPath}'.");
                }
                return prepared.SerializedBytes.ToArray();
            }
        }

        /// <summary>
        /// Attempts to read one prepared output without consulting its live
        /// destination. This internal companion lets session material loading
        /// distinguish an unstaged published asset from a staged one.
        /// </summary>
        internal bool TryReadStagedFile(string projectRelativePath, out byte[] bytes) {
            lock (StateGate) {
                EnsureStaging();
                string normalizedPath = NormalizeProjectRelativePath(projectRelativePath);
                if (!PreparedByPath.TryGetValue(normalizedPath, out EditorPreparedAssetWrite prepared)) {
                    bytes = null;
                    return false;
                }

                bytes = prepared.SerializedBytes.ToArray();
                return true;
            }
        }

        /// <summary>
        /// Captures the exact raw-byte hash currently present at a
        /// project-relative generated-file destination. Null means absent.
        /// </summary>
        public string GetCurrentFileHash(string projectRelativePath) {
            lock (StateGate) {
                EnsureStaging();
                string normalizedPath = NormalizeProjectRelativePath(projectRelativePath);
                string fullPath = Path.Combine(ProjectRootPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, ProjectRootPath);
                if (!File.Exists(fullPath)) {
                    return null;
                }
                return EditorNativeAssetWriteService.ComputeRawBytesHash(
                    EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, fullPath));
            }
        }

        /// <summary>
        /// Stages a generated material's common and platform settings documents
        /// under this transaction and publishes them together on commit.
        /// </summary>
        public EditorAssetWriteResult WriteMaterial(
            string relativePath,
            GeneratedMaterialAssetDefinition definition) {
            lock (StateGate) {
                EnsureStaging();
                EditorPreparedMaterialWrite prepared = NativeWriter.PrepareMaterial(relativePath, definition);
                EditorAssetWriteResult result = StagePrepared(prepared.Common);
                for (int index = 0; index < prepared.Overrides.Count; index++) {
                    StagePrepared(prepared.Overrides[index]);
                }
                return result;
            }
        }

        /// <summary>
        /// Creates a canonical reference to an output already staged by this
        /// transaction.  This keeps generated assets referentially usable
        /// before publication while preserving the transaction's exact
        /// identity and content hash.
        /// </summary>
        /// <param name="relativePath">Assets-relative staged output path.</param>
        /// <param name="expectedKind">Expected authored asset category.</param>
        /// <returns>Canonical reference for the staged output.</returns>
        public SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) {
            lock (StateGate) {
                EnsureStaging();
                string normalizedPath = NormalizeRelativePath(relativePath);
                if (!TryCreatePreparedReference(normalizedPath, expectedKind, out SceneAssetReference reference)) {
                    throw new InvalidOperationException(
                        $"The transaction has not staged authored output '{normalizedPath}'.");
                }

                return reference;
            }
        }

        /// <summary>
        /// Attempts to create a canonical reference to an output already
        /// staged by this transaction.  A false result means callers may
        /// resolve an existing, previously published destination through the
        /// session identity graph.
        /// </summary>
        public bool TryCreateReference(
            string relativePath,
            AssetEntryKind expectedKind,
            out SceneAssetReference reference) {
            lock (StateGate) {
                EnsureStaging();
                return TryCreatePreparedReference(NormalizeRelativePath(relativePath), expectedKind, out reference);
            }
        }

        SceneAssetReference CreatePreparedReference(EditorPreparedAssetWrite prepared, string normalizedPath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                prepared.AssetId,
                normalizedPath,
                prepared.ContentHash);
        }

        bool TryCreatePreparedReference(
            string normalizedPath,
            AssetEntryKind expectedKind,
            out SceneAssetReference reference) {
            if (!PreparedByPath.TryGetValue(normalizedPath, out EditorPreparedAssetWrite prepared)) {
                reference = null;
                return false;
            }

            string preparedAssetKind = GetPreparedAssetKind(expectedKind);
            bool kindMatches = string.Equals(prepared.AssetKind, preparedAssetKind, StringComparison.Ordinal)
                || (expectedKind == AssetEntryKind.Material
                    && string.Equals(prepared.AssetKind, "MaterialAssetCommonSettingsDocument", StringComparison.Ordinal));
            if (!kindMatches) {
                throw new InvalidOperationException(
                    $"The staged output '{normalizedPath}' is a {prepared.AssetKind} asset, not an {expectedKind} asset.");
            }

            reference = CreatePreparedReference(prepared, normalizedPath);
            return true;
        }

        static string GetPreparedAssetKind(AssetEntryKind expectedKind) {
            return expectedKind switch {
                AssetEntryKind.Image => "TextureAsset",
                AssetEntryKind.Model => "ModelAsset",
                AssetEntryKind.Material => "MaterialAsset",
                AssetEntryKind.Scene => "SceneAsset",
                AssetEntryKind.Blueprint => "BlueprintAsset",
                AssetEntryKind.Audio => "AudioAsset",
                _ => throw new InvalidOperationException($"Staged references are not supported for asset kind '{expectedKind}'.")
            };
        }

        EditorAssetWriteResult StagePrepared(EditorPreparedAssetWrite prepared) {
            if (prepared == null) {
                throw new ArgumentNullException(nameof(prepared));
            }

            EditorAuthoringTransactionEntry existingEntry = Document.Entries.FirstOrDefault(entry =>
                string.Equals(entry.DestinationRelativePath, prepared.RelativePath, PathComparison));
            string stagedRelativePath = existingEntry?.StagedRelativePath ??
                Path.Combine("staged", Document.Entries.Count.ToString("D8") + ".payload").Replace(Path.DirectorySeparatorChar, '/');
            string stagedPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                TransactionDirectoryPath,
                stagedRelativePath,
                "staged");
            WriteBytesDurably(stagedPath, prepared.SerializedBytes, TransactionDirectoryPath);

            EditorAuthoringTransactionEntry entry = existingEntry ?? new EditorAuthoringTransactionEntry {
                DestinationRelativePath = prepared.RelativePath,
                StagedRelativePath = stagedRelativePath,
                PriorExists = prepared.PriorExists,
                PriorContentHash = prepared.PriorContentHash,
                PriorSerializedHash = prepared.PriorSerializedHash,
                BackupRelativePath = prepared.PriorExists
                    ? Path.Combine("backups", Document.Entries.Count.ToString("D8") + ".payload").Replace(Path.DirectorySeparatorChar, '/')
                    : null
            };
            entry.StagedContentHash = prepared.ContentHash;
            entry.StagedSerializedHash = prepared.SerializedHash;
            entry.ExpectedAssetId = prepared.AssetId;
            entry.ExpectedAssetKind = prepared.AssetKind;
            entry.PayloadKind = prepared.PayloadKind;
            entry.UsesProjectRoot = prepared.UsesProjectRoot;
            entry.IsMaterialSettingsPayload = prepared.IsMaterialSettingsPayload;
            entry.UpdatesIdentityIndex = prepared.UpdatesIdentityIndex;
            entry.Changed = !prepared.IsUnchanged;
            entry.Progress = entry.Changed ? EditorAuthoringTransactionEntryProgress.Staged : EditorAuthoringTransactionEntryProgress.Skipped;
            entry.State = EditorAuthoringTransactionState.Staging;
            if (existingEntry == null) {
                Document.Entries.Add(entry);
            }
            PreparedByPath[prepared.RelativePath] = prepared;
            WriteDocument();

            EditorAssetWriteDisposition disposition = prepared.PriorExists
                ? (prepared.IsUnchanged ? EditorAssetWriteDisposition.Unchanged : EditorAssetWriteDisposition.Changed)
                : EditorAssetWriteDisposition.Created;
            return new EditorAssetWriteResult(
                prepared.RelativePath,
                prepared.FullPath,
                prepared.AssetId,
                prepared.ContentHash,
                disposition,
                prepared.PreservedExistingIdentity);
        }

        /// <summary>
        /// Publishes all changed staged outputs under the project authoring lock.
        /// </summary>
        public void Commit() {
            lock (StateGate) {
                if (OutcomeValue == EditorAuthoringTransactionOutcome.Committed) {
                    return;
                }
                if (OutcomeValue == EditorAuthoringTransactionOutcome.Disposed) {
                    throw new ObjectDisposedException(nameof(EditorAuthoringTransaction));
                }
                if (OutcomeValue == EditorAuthoringTransactionOutcome.RolledBack) {
                    throw new InvalidOperationException("The authoring transaction has already rolled back.");
                }
                if (OutcomeValue == EditorAuthoringTransactionOutcome.Failed) {
                    throw new InvalidOperationException("The authoring transaction failed and requires recovery.");
                }
                EnsureStaging();
                using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
                NativeWriter.ReconcileCommittedChangesUnderLock();
                NativeWriter.ValidatePreparedIdentityClaimsUnderLock(PreparedByPath.Values.ToArray());
                ValidateStagedEntriesUnderLock();
                List<EditorAuthoringTransactionEntry> changedEntries = Document.Entries
                    .Where(entry => entry.Changed)
                    .ToList();
                EditorAuthoringTransactionEntry[] nativeChangedEntries = changedEntries
                    .Where(entry => !entry.UsesProjectRoot)
                    .ToArray();
                List<EditorAuthoringTransactionEntry> appliedEntries = new List<EditorAuthoringTransactionEntry>();
                IDisposable pendingOwner = null;
                bool committedDurably = false;
                bool generationPublished = false;
                try {
                    for (int index = 0; index < changedEntries.Count; index++) {
                        EditorAuthoringTransactionEntry entry = changedEntries[index];
                        EditorPreparedAssetWrite prepared = PreparedByPath[entry.DestinationRelativePath];
                        if (entry.PriorExists) {
                            string backupPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                                TransactionDirectoryPath,
                                entry.BackupRelativePath,
                                "backup");
                            byte[] priorBytes = EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, prepared.FullPath);
                            if (!string.Equals(ComputeCurrentContentHash(entry, prepared, priorBytes), entry.PriorContentHash, StringComparison.Ordinal) ||
                                !string.Equals(NativeWriter.ComputeSerializedHash(priorBytes), entry.PriorSerializedHash, StringComparison.Ordinal)) {
                                throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after validation.");
                            }
                            WriteBytesDurably(backupPath, priorBytes, TransactionDirectoryPath);
                            entry.BackupContentHash = entry.PriorContentHash;
                            entry.BackupSerializedHash = NativeWriter.ComputeSerializedHash(priorBytes);
                            WriteDocument();
                        }
                    }

                    Document.State = EditorAuthoringTransactionState.Committing;
                    for (int index = 0; index < Document.Entries.Count; index++) {
                        Document.Entries[index].State = Document.State;
                    }
                    WriteDocument();
                    if (changedEntries.Count > 0) {
                        EditorAuthoringTransactionPendingMarker.PublishUnderLock(ProjectRootPath, Document.TransactionId,
                            changedEntries.Select(entry => entry.DestinationRelativePath).ToArray());
                        pendingOwner = EditorAuthoringTransactionPendingMarker.EnterOwner(ProjectRootPath, Document.TransactionId);
                        for (int index = 0; index < changedEntries.Count; index++) {
                            string directoryPath = Path.GetDirectoryName(PreparedByPath[changedEntries[index].DestinationRelativePath].FullPath);
                            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                                EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, directoryPath);
                            }
                        }
                    }
                    for (int index = 0; index < changedEntries.Count; index++) {
                        EditorAuthoringTransactionEntry entry = changedEntries[index];
                        EditorPreparedAssetWrite prepared = PreparedByPath[entry.DestinationRelativePath];
                        Hooks.BeforeReplacement?.Invoke(index, prepared.FullPath);
                        entry.Progress = EditorAuthoringTransactionEntryProgress.Applying;
                        WriteDocument();
                        string stagedPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                            TransactionDirectoryPath,
                            entry.StagedRelativePath,
                            "staged");
                        byte[] stagedBytes = EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, stagedPath);
                        ValidatePreparedPayload(entry, prepared, stagedBytes);
                        appliedEntries.Add(entry);
                        EditorAuthoringTransactionRecoveryService.ReplaceAtomically(
                            prepared.FullPath,
                            stagedBytes,
                            prepared.UsesProjectRoot ? ProjectRootPath : AssetsRootPath,
                            ProjectRootPath);
                        entry.Progress = EditorAuthoringTransactionEntryProgress.Applied;
                        WriteDocument();
                        Hooks.AfterReplacement?.Invoke(index, prepared.FullPath);
                    }

                    for (int index = 0; index < changedEntries.Count; index++) {
                        Hooks.BeforePublication?.Invoke(index, changedEntries[index].DestinationRelativePath);
                    }
                    if (nativeChangedEntries.Length > 0) {
                        NativeWriter.PublishChangesUnderLock(nativeChangedEntries.Select(entry => entry.DestinationRelativePath).ToArray());
                        generationPublished = true;
                    }
                    for (int index = 0; index < changedEntries.Count; index++) {
                        EditorAuthoringTransactionEntry entry = changedEntries[index];
                        Hooks.BeforeGraphUpdate?.Invoke(index, entry.DestinationRelativePath);
                        NativeWriter.ApplyPublishedAssetUnderLock(PreparedByPath[entry.DestinationRelativePath]);
                    }
                    // Identity/index/hash registration is intentionally after
                    // publication. Readers cannot observe a graph entry whose
                    // destination has not become durable yet.
                    NativeWriter.FlushHashCacheAtCommit();
                    NativeWriter.ObserveCurrentGenerationUnderLock();
                    Hooks.AfterPublication?.Invoke();
                    Document.State = EditorAuthoringTransactionState.Committed;
                    for (int index = 0; index < Document.Entries.Count; index++) {
                        Document.Entries[index].State = Document.State;
                    }
                    WriteDocument();
                    committedDurably = true;
                    Hooks.BeforePendingMarkerClear?.Invoke();
                    EditorAuthoringTransactionPendingMarker.ClearUnderLock(ProjectRootPath, Document.TransactionId);
                    pendingOwner?.Dispose();
                    pendingOwner = null;
                    try {
                        Hooks.BeforeCleanup?.Invoke();
                        DeleteOwnDirectory();
                    } catch {
                        CloseLease();
                    }
                    Complete(EditorAuthoringTransactionOutcome.Committed);
                } catch (Exception primaryException) {
                    if (committedDurably) {
                        pendingOwner?.Dispose();
                        CloseLease();
                        Complete(EditorAuthoringTransactionOutcome.Committed);
                        throw new InvalidOperationException("The authoring transaction committed but could not clear its pending marker.", primaryException);
                    }
                    Exception rollbackException = null;
                    try {
                        rollbackException = RollbackUnderLock(appliedEntries);
                        if (rollbackException == null) {
                            try {
                                // Persist rollback tombstones before releasing the marker so
                                // a newly opened observer cannot reuse a failed publication hash.
                                NativeWriter.FlushHashCacheAtCommit();
                            } catch (Exception cacheRollbackException) {
                                rollbackException = cacheRollbackException;
                            }
                        }
                        if (rollbackException == null && generationPublished && nativeChangedEntries.Length > 0) {
                            try {
                                NativeWriter.PublishRollbackChangesUnderLock(
                                    Document.TransactionId,
                                    nativeChangedEntries.Select(entry => entry.DestinationRelativePath).ToArray());
                                NativeWriter.ObserveCurrentGenerationUnderLock();
                            } catch (Exception generationRollbackException) {
                                rollbackException = generationRollbackException;
                            }
                        }
                        if (rollbackException == null) {
                            // Aborting is a durable recovery state. It prevents a
                            // pending marker from ever describing a staging journal.
                            Document.State = EditorAuthoringTransactionState.Aborting;
                            for (int index = 0; index < Document.Entries.Count; index++) {
                                Document.Entries[index].State = Document.State;
                            }
                            WriteDocument();
                            Document.State = EditorAuthoringTransactionState.RolledBack;
                            for (int index = 0; index < Document.Entries.Count; index++) {
                                Document.Entries[index].State = Document.State;
                            }
                            WriteDocument();
                            Hooks.BeforePendingMarkerClear?.Invoke();
                            EditorAuthoringTransactionPendingMarker.ClearUnderLock(ProjectRootPath, Document.TransactionId);
                            if (generationPublished && nativeChangedEntries.Length > 0) {
                                NativeWriter.PruneRollbackChangesUnderLock(Document.TransactionId);
                            }
                            try {
                                DeleteOwnDirectory();
                            } catch {
                                CloseLease();
                            }
                            Complete(EditorAuthoringTransactionOutcome.RolledBack);
                        }
                    } catch (Exception rollbackFailure) {
                        rollbackException ??= rollbackFailure;
                    } finally {
                        try {
                            pendingOwner?.Dispose();
                        } catch (Exception ownerFailure) {
                            rollbackException ??= ownerFailure;
                        }
                    }

                    if (rollbackException != null) {
                        OutcomeValue = EditorAuthoringTransactionOutcome.Failed;
                        throw new AggregateException("Authoring transaction publication and rollback failed; the journal remains for recovery.", primaryException, rollbackException);
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Removes uncommitted staging owned by this transaction.
        /// </summary>
        public void Dispose() {
            lock (StateGate) {
                if (IsDisposed) {
                    return;
                }

                if (Document.State == EditorAuthoringTransactionState.Staging &&
                    OutcomeValue == EditorAuthoringTransactionOutcome.Active) {
                    using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
                    DeleteOwnDirectory();
                } else {
                    // A journal retained for recovery still owns a live lease until
                    // its session is released; leave the journal and marker intact.
                    CloseLease();
                }
                Complete(OutcomeValue == EditorAuthoringTransactionOutcome.Failed
                    ? EditorAuthoringTransactionOutcome.Failed
                    : EditorAuthoringTransactionOutcome.Disposed);
            }
        }

        void ValidateStagedEntriesUnderLock() {
            HashSet<string> paths = new HashSet<string>(PathComparer);
            for (int index = 0; index < Document.Entries.Count; index++) {
                EditorAuthoringTransactionEntry entry = Document.Entries[index];
                if (entry == null || !paths.Add(entry.DestinationRelativePath)) {
                    throw new InvalidDataException("The authoring transaction contains duplicate destinations.");
                }
                if (!PreparedByPath.TryGetValue(entry.DestinationRelativePath, out EditorPreparedAssetWrite prepared)) {
                    throw new InvalidDataException("The authoring transaction contains a destination without a prepared output.");
                }

                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                    GetDestinationRoot(entry),
                    entry.DestinationRelativePath,
                    "destination");
                string stagedPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, entry.StagedRelativePath, "staged");
                byte[] stagedBytes = EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, stagedPath);
                ValidatePreparedPayload(entry, prepared, stagedBytes);
                bool currentExists = File.Exists(prepared.FullPath);
                if (currentExists != entry.PriorExists) {
                    throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after staging.");
                }
                if (currentExists) {
                    byte[] currentBytes = EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, prepared.FullPath);
                    string currentHash = ComputeCurrentContentHash(entry, prepared, currentBytes);
                    string currentSerializedHash = NativeWriter.ComputeSerializedHash(currentBytes);
                    if (!string.Equals(currentHash, entry.PriorContentHash, StringComparison.Ordinal) ||
                        !string.Equals(currentSerializedHash, entry.PriorSerializedHash, StringComparison.Ordinal)) {
                        throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after staging.");
                    }
                }
            }
        }

        void ValidatePreparedPayload(
            EditorAuthoringTransactionEntry entry,
            EditorPreparedAssetWrite prepared,
            byte[] stagedBytes) {
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile) {
                EditorNativeAssetWriteService.ValidateGeneratedFilePayload(
                    stagedBytes,
                    entry.StagedContentHash,
                    entry.ExpectedAssetKind);
                return;
            }

            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride ||
                entry.IsMaterialSettingsPayload) {
                NativeWriter.ValidatePreparedMaterialPayload(
                    stagedBytes,
                    entry.StagedContentHash,
                    entry.StagedSerializedHash,
                    entry.ExpectedAssetId,
                    entry.ExpectedAssetKind);
                return;
            }

            NativeWriter.ValidatePreparedPayload(
                stagedBytes,
                prepared.FullPath,
                entry.StagedContentHash,
                entry.StagedSerializedHash,
                entry.ExpectedAssetId,
                entry.ExpectedAssetKind);
        }

        string ComputeCurrentContentHash(
            EditorAuthoringTransactionEntry entry,
            EditorPreparedAssetWrite prepared,
            byte[] currentBytes) {
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile) {
                return EditorNativeAssetWriteService.ComputeRawBytesHash(currentBytes);
            }
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.NativeAsset && entry.IsMaterialSettingsPayload)) {
                return EditorNativeAssetWriteService.ComputeCanonicalMaterialSettingsHash(currentBytes);
            }
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride) {
                return NativeWriter.ComputeSerializedHash(currentBytes);
            }
            return NativeWriter.ComputeCurrentContentHash(prepared.FullPath);
        }

        Exception RollbackUnderLock(IReadOnlyList<EditorAuthoringTransactionEntry> appliedEntries) {
            List<Exception> failures = new List<Exception>();
            List<LiveRollbackOperation> operations = new List<LiveRollbackOperation>();

            // Prove every destination and backup before mutating any one of
            // them. This keeps a divergent later destination from producing a
            // partially restored live rollback.
            for (int index = appliedEntries.Count - 1; index >= 0; index--) {
                EditorAuthoringTransactionEntry entry = appliedEntries[index];
                try {
                    EditorPreparedAssetWrite prepared = PreparedByPath[entry.DestinationRelativePath];
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                        prepared.FullPath,
                        GetDestinationRoot(entry));
                    byte[] currentBytes = File.Exists(prepared.FullPath)
                        ? EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, prepared.FullPath)
                        : null;
                    bool replacementApplied = false;
                    if (currentBytes != null) {
                        string currentHash = NativeWriter.ComputeSerializedHash(currentBytes);
                        replacementApplied = string.Equals(currentHash, entry.StagedSerializedHash, StringComparison.Ordinal);
                        bool isPrior = entry.PriorExists && string.Equals(currentHash, entry.PriorSerializedHash, StringComparison.Ordinal);
                        if (!replacementApplied && !isPrior) {
                            throw new InvalidDataException($"The transaction destination '{prepared.FullPath}' changed after publication failed.");
                        }
                    } else if (entry.PriorExists) {
                        throw new InvalidDataException($"The transaction destination '{prepared.FullPath}' disappeared after publication failed.");
                    }

                    byte[] backupBytes = null;
                    if (entry.PriorExists) {
                        string backupPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                            TransactionDirectoryPath,
                            entry.BackupRelativePath,
                            "backup");
                        backupBytes = EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, backupPath);
                        if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile) {
                            EditorNativeAssetWriteService.ValidateGeneratedFilePayload(
                                backupBytes,
                                entry.BackupContentHash ?? entry.PriorContentHash,
                                entry.ExpectedAssetKind);
                        } else if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                                   entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride ||
                                   entry.IsMaterialSettingsPayload) {
                            NativeWriter.ValidatePreparedMaterialPayload(
                                backupBytes,
                                entry.BackupContentHash ?? entry.PriorContentHash,
                                entry.BackupSerializedHash ?? entry.PriorSerializedHash,
                                entry.ExpectedAssetId,
                                entry.ExpectedAssetKind);
                        } else {
                            EditorNativeAssetWriteService.ValidateNativePayloadIntegrity(
                                backupBytes,
                                prepared.FullPath,
                                entry.BackupContentHash ?? entry.PriorContentHash,
                                entry.BackupSerializedHash ?? entry.PriorSerializedHash,
                                entry.ExpectedAssetId,
                                entry.ExpectedAssetKind);
                        }
                    }
                    operations.Add(new LiveRollbackOperation(entry, prepared, replacementApplied, backupBytes));
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }

            if (failures.Count == 0) {
                for (int index = 0; index < operations.Count; index++) {
                    LiveRollbackOperation operation = operations[index];
                    try {
                        Hooks.BeforeRollback?.Invoke(index, operation.Prepared.FullPath);
                        bool currentlyStaged = false;
                        if (operation.ReplacementApplied) {
                            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                                operation.Prepared.FullPath,
                                GetDestinationRoot(operation.Entry));
                            byte[] currentBytes = File.Exists(operation.Prepared.FullPath)
                                ? EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, operation.Prepared.FullPath)
                                : null;
                            if (currentBytes == null) {
                                if (operation.Entry.PriorExists) {
                                    throw new InvalidDataException($"The authoring transaction destination '{operation.Prepared.FullPath}' disappeared during rollback.");
                                }
                            } else {
                                string currentHash = NativeWriter.ComputeSerializedHash(currentBytes);
                                currentlyStaged = string.Equals(currentHash, operation.Entry.StagedSerializedHash, StringComparison.Ordinal);
                                bool currentlyPrior = operation.Entry.PriorExists &&
                                    string.Equals(currentHash, operation.Entry.PriorSerializedHash, StringComparison.Ordinal);
                                if (!currentlyStaged && !currentlyPrior) {
                                    throw new InvalidDataException($"The authoring transaction destination '{operation.Prepared.FullPath}' changed during rollback.");
                                }
                            }
                        }
                        if (currentlyStaged) {
                            if (operation.Entry.PriorExists) {
                                EditorAuthoringTransactionRecoveryService.ReplaceAtomically(
                                    operation.Prepared.FullPath,
                                    operation.BackupBytes,
                                    GetDestinationRoot(operation.Entry),
                                    ProjectRootPath);
                            } else if (File.Exists(operation.Prepared.FullPath)) {
                                using EditorAuthoringMutationScope mutationScope = EditorAuthoringMutationScope.AcquireForMutation(
                                    ProjectRootPath,
                                    Path.GetDirectoryName(operation.Prepared.FullPath));
                                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                                    operation.Prepared.FullPath,
                                    GetDestinationRoot(operation.Entry));
                                EditorAuthoringMutationScope.DeleteLeaf(ProjectRootPath, operation.Prepared.FullPath);
                            }
                        }
                        NativeWriter.RestorePublishedAssetUnderLock(operation.Prepared);
                    } catch (Exception exception) {
                        failures.Add(exception);
                    }
                }
            }

            return failures.Count == 0
                ? null
                : new AggregateException("One or more authoring transaction destinations could not be restored.", failures);
        }

        sealed class LiveRollbackOperation {
            public LiveRollbackOperation(
                EditorAuthoringTransactionEntry entry,
                EditorPreparedAssetWrite prepared,
                bool replacementApplied,
                byte[] backupBytes) {
                Entry = entry;
                Prepared = prepared;
                ReplacementApplied = replacementApplied;
                BackupBytes = backupBytes;
            }

            public EditorAuthoringTransactionEntry Entry { get; }

            public EditorPreparedAssetWrite Prepared { get; }

            public bool ReplacementApplied { get; }

            public byte[] BackupBytes { get; }
        }

        void DeleteOwnDirectory() {
            string transactionRoot = EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ResolveContainedPath(transactionRoot, Document.TransactionId, "transaction");
            // Retirement is a publication-root mutation even for an uncommitted
            // staging transaction. Reentrant acquisition is borrowed during Commit.
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
            using EditorAuthoringMutationScope mutationScope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                transactionRoot);

            if (RetiredDirectory) {
                if (!Directory.Exists(TransactionDirectoryPath)) {
                    return;
                }

                EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(TransactionDirectoryPath, transactionRoot);
                EditorAuthoringMutationScope.DeleteDirectoryTree(ProjectRootPath, TransactionDirectoryPath, transactionRoot);
                return;
            }

            if (!Directory.Exists(TransactionDirectoryPath)) {
                return;
            }

            EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(TransactionDirectoryPath, transactionRoot);
            string deletingDirectory = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                transactionRoot,
                ".deleting-" + Document.TransactionId,
                "terminal deletion");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(deletingDirectory, transactionRoot);
            Hooks.BeforeRetireRename?.Invoke();
            // Windows cannot move a directory containing an open non-shareable
            // lease handle. The project lock excludes cooperating recovery
            // while this brief close-and-rename sequence runs; if the rename
            // itself fails, immediately reacquire the lease so this instance
            // remains the sole owner and Dispose can retry deterministically.
            CloseLease();
            try {
                EditorAuthoringMutationScope.MoveDirectory(ProjectRootPath, TransactionDirectoryPath, deletingDirectory);
            } catch (Exception renameException) {
                try {
                    OpenLease();
                } catch (Exception leaseException) {
                    throw new AggregateException("Authoring transaction retirement failed and its lease could not be reacquired.", renameException, leaseException);
                }
                throw;
            }
            RetiredDirectory = true;
            TransactionDirectoryPath = deletingDirectory;
            Hooks.AfterRetireRename?.Invoke();
            EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(TransactionDirectoryPath, transactionRoot);
            EditorAuthoringMutationScope.DeleteDirectoryTree(ProjectRootPath, TransactionDirectoryPath, transactionRoot);
        }

        void CloseLease() {
            if (LeaseFile == null && LeaseMutationScope == null) {
                return;
            }

            List<Exception> failures = new List<Exception>();
            try {
                LeaseFile?.Dispose();
            } catch (Exception exception) {
                failures.Add(exception);
            }
            try {
                LeaseMutationScope?.Dispose();
            } catch (Exception exception) {
                failures.Add(exception);
            }
            if (failures.Count > 0) {
                throw new AggregateException("The authoring transaction lease could not be released.", failures);
            }
            LeaseFile = null;
            LeaseMutationScope = null;
            LeaseStream = null;
        }

        void OpenLease() {
            string leasePath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                TransactionDirectoryPath,
                "lease",
                "lease");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(leasePath, TransactionDirectoryPath);
            EditorAuthoringMutationScope mutationScope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                TransactionDirectoryPath);
            EditorAuthoringVerifiedFile leaseFile = null;
            try {
                leaseFile = mutationScope.OpenVerifiedFile(
                    leasePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                if (!mutationScope.TryAcquireExclusiveFileLock(leaseFile)) {
                    throw new IOException("The authoring transaction lease is held by another owner.");
                }
                LeaseMutationScope = mutationScope;
                LeaseFile = leaseFile;
                LeaseStream = leaseFile.Stream;
                mutationScope = null;
                leaseFile = null;
            } finally {
                leaseFile?.Dispose();
                mutationScope?.Dispose();
            }
        }

        internal void ReleaseLeaseForTesting() {
            lock (StateGate) {
                CloseLease();
            }
        }

        void Complete(EditorAuthoringTransactionOutcome outcome) {
            if (IsDisposed) {
                return;
            }
            OutcomeValue = outcome;
            IsDisposed = true;
            CompletionCallback();
        }

        void WriteDocument() {
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                TransactionDirectoryPath,
                EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath));
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(
                ManifestPath,
                TransactionDirectoryPath);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(Document, EditorAuthoringTransactionDocument.JsonOptions);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, ManifestPath, bytes);
        }

        void WriteBytesDurably(string path, byte[] bytes, string containingRoot) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, containingRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, containingRoot);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, path, bytes);
        }

        static string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            }
            return relativePath.Replace('\\', '/').Trim('/');
        }

        static string NormalizeProjectRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Generated file path must be project-relative.", nameof(relativePath));
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Equals("..", StringComparison.Ordinal)
                || normalized.StartsWith("../", StringComparison.Ordinal)
                || normalized.Contains("/../", StringComparison.Ordinal)) {
                throw new ArgumentException("Generated file path must be canonical and project-relative.", nameof(relativePath));
            }
            return normalized;
        }

        string GetDestinationRoot(EditorAuthoringTransactionEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            return entry.UsesProjectRoot ? ProjectRootPath : AssetsRootPath;
        }

        static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        void EnsureStaging() {
            if (IsDisposed || OutcomeValue != EditorAuthoringTransactionOutcome.Active ||
                RetiredDirectory || Document.State != EditorAuthoringTransactionState.Staging) {
                throw new ObjectDisposedException(nameof(EditorAuthoringTransaction));
            }
        }
    }

    /// <summary>
    /// Injectable publication seams used by deterministic transaction tests.
    /// </summary>
    internal sealed class EditorAuthoringTransactionHooks {
        public Action BeforeManifestWrite { get; init; }

        public Action<int, string> BeforeReplacement { get; init; }

        public Action<int, string> AfterReplacement { get; init; }

        public Action<int, string> BeforeGraphUpdate { get; init; }

        public Action<int, string> BeforePublication { get; init; }

        public Action<int, string> BeforeRollback { get; init; }

        public Action AfterPublication { get; init; }

        public Action BeforePendingMarkerClear { get; init; }

        public Action BeforeCleanup { get; init; }

        public Action BeforeRetireRename { get; init; }

        public Action AfterRetireRename { get; init; }
    }
}
