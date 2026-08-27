using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Stages current native asset outputs and publishes them as one recoverable batch.
    /// </summary>
    public sealed class EditorAuthoringTransaction : IDisposable {
        readonly string ProjectRootPath;
        readonly string AssetsRootPath;
        readonly string TransactionDirectoryPath;
        readonly string ManifestPath;
        readonly EditorNativeAssetWriteService NativeWriter;
        readonly Action CompletionCallback;
        readonly EditorAuthoringTransactionHooks Hooks;
        readonly Dictionary<string, EditorPreparedAssetWrite> PreparedByPath;
        readonly object StateGate = new object();
        FileStream LeaseStream;
        EditorAuthoringTransactionDocument Document;
        bool IsDisposed;
        bool IsCompleted;

        internal EditorAuthoringTransaction(
            string projectRootPath,
            EditorNativeAssetWriteService nativeWriter,
            Action completionCallback,
            EditorAuthoringTransactionHooks hooks = null) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            NativeWriter = nativeWriter ?? throw new ArgumentNullException(nameof(nativeWriter));
            CompletionCallback = completionCallback ?? throw new ArgumentNullException(nameof(completionCallback));
            Hooks = hooks ?? new EditorAuthoringTransactionHooks();
            PreparedByPath = new Dictionary<string, EditorPreparedAssetWrite>(PathComparer);

            string transactionRoot = EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(ProjectRootPath);
            Directory.CreateDirectory(transactionRoot);
            string transactionId = Guid.NewGuid().ToString("N");
            TransactionDirectoryPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(transactionRoot, transactionId, "transaction");
            ManifestPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "transaction.json", "manifest");
            try {
                Directory.CreateDirectory(TransactionDirectoryPath);
                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "staged", "staged-root");
                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "backups", "backup-root");
                Directory.CreateDirectory(Path.Combine(TransactionDirectoryPath, "staged"));
                Directory.CreateDirectory(Path.Combine(TransactionDirectoryPath, "backups"));
                string leasePath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, "lease", "lease");
                LeaseStream = new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough);
                Document = new EditorAuthoringTransactionDocument {
                    TransactionId = transactionId,
                    State = EditorAuthoringTransactionState.Staging
                };
                WriteDocument();
            } catch (Exception primaryException) {
                try {
                    LeaseStream?.Dispose();
                    LeaseStream = null;
                    if (Directory.Exists(TransactionDirectoryPath)) {
                        EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(TransactionDirectoryPath, transactionRoot);
                        Directory.Delete(TransactionDirectoryPath, true);
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
        /// Gets the current durable transaction state.
        /// </summary>
        public EditorAuthoringTransactionState State => Document.State;

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

                EditorPreparedAssetWrite prepared = NativeWriter.PrepareAsset(relativePath, asset);
                EditorAuthoringTransactionEntry existingEntry = Document.Entries.FirstOrDefault(entry =>
                    string.Equals(entry.DestinationRelativePath, prepared.RelativePath, PathComparison));
                string stagedRelativePath = existingEntry?.StagedRelativePath ??
                    Path.Combine("staged", Document.Entries.Count.ToString("D8") + ".payload").Replace(Path.DirectorySeparatorChar, '/');
                string stagedPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                    TransactionDirectoryPath,
                    stagedRelativePath,
                    "staged");
                WriteBytesDurably(stagedPath, prepared.SerializedBytes);

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
        }

        /// <summary>
        /// Publishes all changed staged outputs under the project authoring lock.
        /// </summary>
        public void Commit() {
            lock (StateGate) {
                if (IsCompleted) {
                    return;
                }
                EnsureStaging();
                using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
                ValidateStagedEntriesUnderLock();
                List<EditorAuthoringTransactionEntry> changedEntries = Document.Entries
                    .Where(entry => entry.Changed)
                    .ToList();
                List<EditorAuthoringTransactionEntry> appliedEntries = new List<EditorAuthoringTransactionEntry>();
                IDisposable pendingOwner = null;
                bool committedDurably = false;
                try {
                    for (int index = 0; index < changedEntries.Count; index++) {
                        EditorAuthoringTransactionEntry entry = changedEntries[index];
                        EditorPreparedAssetWrite prepared = PreparedByPath[entry.DestinationRelativePath];
                        if (entry.PriorExists) {
                            string backupPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                                TransactionDirectoryPath,
                                entry.BackupRelativePath,
                                "backup");
                            byte[] priorBytes = File.ReadAllBytes(prepared.FullPath);
                            if (!string.Equals(NativeWriter.ComputeCurrentContentHash(prepared.FullPath), entry.PriorContentHash, StringComparison.Ordinal) ||
                                !string.Equals(NativeWriter.ComputeSerializedHash(priorBytes), entry.PriorSerializedHash, StringComparison.Ordinal)) {
                                throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after validation.");
                            }
                            WriteBytesDurably(backupPath, priorBytes);
                            entry.BackupContentHash = entry.PriorContentHash;
                            entry.BackupSerializedHash = NativeWriter.ComputeSerializedHash(priorBytes);
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
                        byte[] stagedBytes = File.ReadAllBytes(stagedPath);
                        NativeWriter.ValidatePreparedPayload(
                            stagedBytes,
                            prepared.FullPath,
                            entry.StagedContentHash,
                            entry.StagedSerializedHash,
                            entry.ExpectedAssetId,
                            entry.ExpectedAssetKind);
                        appliedEntries.Add(entry);
                        EditorAuthoringTransactionRecoveryService.ReplaceAtomically(prepared.FullPath, stagedBytes);
                        entry.Progress = EditorAuthoringTransactionEntryProgress.Applied;
                        WriteDocument();
                        Hooks.AfterReplacement?.Invoke(index, prepared.FullPath);
                    }

                    for (int index = 0; index < changedEntries.Count; index++) {
                        EditorAuthoringTransactionEntry entry = changedEntries[index];
                        Hooks.BeforeGraphUpdate?.Invoke(index, entry.DestinationRelativePath);
                        NativeWriter.ApplyPublishedAssetUnderLock(PreparedByPath[entry.DestinationRelativePath]);
                    }
                    for (int index = 0; index < changedEntries.Count; index++) {
                        Hooks.BeforePublication?.Invoke(index, changedEntries[index].DestinationRelativePath);
                    }
                    NativeWriter.FlushHashCacheAtCommit();
                    if (changedEntries.Count > 0) {
                        NativeWriter.PublishChangesUnderLock(changedEntries.Select(entry => entry.DestinationRelativePath).ToArray());
                    }
                    NativeWriter.ObserveCurrentGenerationUnderLock();
                    Document.State = EditorAuthoringTransactionState.Committed;
                    for (int index = 0; index < Document.Entries.Count; index++) {
                        Document.Entries[index].State = Document.State;
                    }
                    WriteDocument();
                    committedDurably = true;
                    EditorAuthoringTransactionPendingMarker.ClearUnderLock(ProjectRootPath, Document.TransactionId);
                    pendingOwner?.Dispose();
                    pendingOwner = null;
                    try {
                        Hooks.BeforeCleanup?.Invoke();
                        DeleteOwnDirectory();
                    } catch {
                        CloseLease();
                    }
                    Complete();
                } catch (Exception primaryException) {
                    if (committedDurably) {
                        pendingOwner?.Dispose();
                        CloseLease();
                        Complete();
                        throw new InvalidOperationException("The authoring transaction committed but could not clear its pending marker.", primaryException);
                    }
                    Exception rollbackException = committedDurably ? null : RollbackUnderLock(appliedEntries);
                    if (rollbackException == null) {
                        try {
                            // Persist rollback tombstones before releasing the marker so
                            // a newly opened observer cannot reuse a failed publication hash.
                            NativeWriter.FlushHashCacheAtCommit();
                        } catch (Exception cacheRollbackException) {
                            rollbackException = cacheRollbackException;
                        }
                        if (rollbackException == null) {
                            Document.State = EditorAuthoringTransactionState.Staging;
                            for (int index = 0; index < Document.Entries.Count; index++) {
                                Document.Entries[index].State = Document.State;
                            }
                            WriteDocument();
                            try {
                                EditorAuthoringTransactionPendingMarker.ClearUnderLock(ProjectRootPath, Document.TransactionId);
                            } catch (Exception clearException) {
                                rollbackException = clearException;
                            }
                        }
                    }
                    pendingOwner?.Dispose();

                    if (rollbackException != null) {
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
                if (IsDisposed || IsCompleted) {
                    return;
                }

                if (Document.State == EditorAuthoringTransactionState.Staging) {
                    DeleteOwnDirectory();
                } else {
                    // A journal retained for recovery still owns a live lease until
                    // its session is released; leave the journal and marker intact.
                    CloseLease();
                }
                Complete();
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

                EditorAuthoringTransactionRecoveryService.ResolveContainedPath(AssetsRootPath, entry.DestinationRelativePath, "destination");
                string stagedPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(TransactionDirectoryPath, entry.StagedRelativePath, "staged");
                byte[] stagedBytes = File.ReadAllBytes(stagedPath);
                NativeWriter.ValidatePreparedPayload(
                    stagedBytes,
                    prepared.FullPath,
                    entry.StagedContentHash,
                    entry.StagedSerializedHash,
                    entry.ExpectedAssetId,
                    entry.ExpectedAssetKind);
                bool currentExists = File.Exists(prepared.FullPath);
                if (currentExists != entry.PriorExists) {
                    throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after staging.");
                }
                if (currentExists) {
                    byte[] currentBytes = File.ReadAllBytes(prepared.FullPath);
                    string currentHash = NativeWriter.ComputeCurrentContentHash(prepared.FullPath);
                    string currentSerializedHash = NativeWriter.ComputeSerializedHash(currentBytes);
                    if (!string.Equals(currentHash, entry.PriorContentHash, StringComparison.Ordinal) ||
                        !string.Equals(currentSerializedHash, entry.PriorSerializedHash, StringComparison.Ordinal)) {
                        throw new IOException($"The authoring transaction destination '{prepared.FullPath}' changed after staging.");
                    }
                }
            }
        }

        Exception RollbackUnderLock(IReadOnlyList<EditorAuthoringTransactionEntry> appliedEntries) {
            List<Exception> failures = new List<Exception>();
            for (int index = appliedEntries.Count - 1; index >= 0; index--) {
                EditorAuthoringTransactionEntry entry = appliedEntries[index];
                try {
                    EditorPreparedAssetWrite prepared = PreparedByPath[entry.DestinationRelativePath];
                    byte[] currentBytes = File.Exists(prepared.FullPath) ? File.ReadAllBytes(prepared.FullPath) : null;
                    if (currentBytes != null) {
                        string currentHash = NativeWriter.ComputeSerializedHash(currentBytes);
                        bool isStaged = string.Equals(currentHash, entry.StagedSerializedHash, StringComparison.Ordinal);
                        bool isPrior = entry.PriorExists && string.Equals(currentHash, entry.PriorSerializedHash, StringComparison.Ordinal);
                        if (!isStaged && !isPrior) {
                            throw new InvalidDataException($"The transaction destination '{prepared.FullPath}' changed after publication failed.");
                        }
                    } else if (entry.PriorExists) {
                        throw new InvalidDataException($"The transaction destination '{prepared.FullPath}' disappeared after publication failed.");
                    }
                    if (entry.PriorExists) {
                        string backupPath = EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                            TransactionDirectoryPath,
                            entry.BackupRelativePath,
                            "backup");
                        byte[] backupBytes = File.ReadAllBytes(backupPath);
                        EditorNativeAssetWriteService.ValidateNativePayloadIntegrity(
                            backupBytes,
                            prepared.FullPath,
                            entry.BackupContentHash ?? entry.PriorContentHash,
                            entry.BackupSerializedHash ?? entry.PriorSerializedHash,
                            entry.ExpectedAssetId,
                            entry.ExpectedAssetKind);
                        EditorAuthoringTransactionRecoveryService.ReplaceAtomically(prepared.FullPath, backupBytes);
                    } else if (File.Exists(prepared.FullPath)) {
                        File.Delete(prepared.FullPath);
                    }
                    NativeWriter.RestorePublishedAssetUnderLock(prepared);
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }

            return failures.Count == 0
                ? null
                : new AggregateException("One or more authoring transaction destinations could not be restored.", failures);
        }

        void DeleteOwnDirectory() {
            EditorAuthoringTransactionRecoveryService.ResolveContainedPath(
                EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath),
                Document.TransactionId,
                "transaction");
            if (Directory.Exists(TransactionDirectoryPath)) {
                CloseLease();
                EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(TransactionDirectoryPath,
                    EditorAuthoringTransactionRecoveryService.GetTransactionRoot(ProjectRootPath));
                Directory.Delete(TransactionDirectoryPath, true);
            }
        }

        void CloseLease() {
            if (LeaseStream == null) {
                return;
            }

            LeaseStream.Dispose();
            LeaseStream = null;
        }

        internal void ReleaseLeaseForTesting() {
            lock (StateGate) {
                CloseLease();
            }
        }

        void Complete() {
            if (IsCompleted) {
                return;
            }
            IsCompleted = true;
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
            string temporaryPath = ManifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(Document, EditorAuthoringTransactionDocument.JsonOptions);
            try {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, ManifestPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        static void WriteBytesDurably(string path, byte[] bytes) {
            string directoryPath = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, path, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        static string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            }
            return relativePath.Replace('\\', '/').Trim('/');
        }

        static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        void EnsureStaging() {
            if (IsDisposed || IsCompleted || Document.State != EditorAuthoringTransactionState.Staging) {
                throw new ObjectDisposedException(nameof(EditorAuthoringTransaction));
            }
        }
    }

    /// <summary>
    /// Injectable publication seams used by deterministic transaction tests.
    /// </summary>
    internal sealed class EditorAuthoringTransactionHooks {
        public Action<int, string> BeforeReplacement { get; init; }

        public Action<int, string> AfterReplacement { get; init; }

        public Action<int, string> BeforeGraphUpdate { get; init; }

        public Action<int, string> BeforePublication { get; init; }

        public Action BeforeCleanup { get; init; }
    }
}
