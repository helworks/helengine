using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Coordinates one project-wide native authoring publication across sessions and processes.
    /// </summary>
    internal sealed class EditorProjectWriteLock : IDisposable {
        static readonly TimeSpan DefaultMaximumWait = TimeSpan.FromSeconds(60);
        const int RetryDelayMilliseconds = 10;
        [ThreadStatic]
        static Dictionary<string, EditorProjectWriteLock> HeldLocks;
        readonly FileStream LockStream;
        readonly string ProjectRootPath;
        readonly bool OwnsHandle;
        bool IsDisposed;

        EditorProjectWriteLock(FileStream lockStream, string projectRootPath, bool ownsHandle) {
            LockStream = lockStream;
            ProjectRootPath = projectRootPath;
            OwnsHandle = ownsHandle;
        }

        /// <summary>
        /// Acquires an exclusive handle for the project's authoring publication boundary.
        /// </summary>
        /// <param name="projectRootPath">Project root to coordinate.</param>
        /// <returns>An exclusive project lock.</returns>
        public static EditorProjectWriteLock Acquire(string projectRootPath) {
            return Acquire(projectRootPath, DefaultMaximumWait);
        }

        /// <summary>
        /// Acquires the project lock using a bounded wait supplied by the caller.
        /// </summary>
        /// <param name="projectRootPath">Project root to coordinate.</param>
        /// <param name="maximumWait">Maximum time to wait for another writer.</param>
        /// <returns>An exclusive project lock.</returns>
        internal static EditorProjectWriteLock Acquire(string projectRootPath, TimeSpan maximumWait) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (maximumWait <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(maximumWait));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullProjectRootPath, fullProjectRootPath);
            string lockPath = Path.Combine(fullProjectRootPath, "cache", "editor", "authoring-write.lock");
            string lockDirectoryPath = Path.GetDirectoryName(lockPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(lockDirectoryPath, fullProjectRootPath);
            Directory.CreateDirectory(lockDirectoryPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(lockDirectoryPath, fullProjectRootPath);
            string canonicalLockPath = CanonicalizeLockPath(lockPath);
            if (HeldLocks != null && HeldLocks.TryGetValue(canonicalLockPath, out EditorProjectWriteLock heldLock)) {
                return new EditorProjectWriteLock(heldLock.LockStream, canonicalLockPath, false);
            }

            IOException lastIOException = null;
            DateTime deadlineUtc = DateTime.UtcNow + maximumWait;
            while (DateTime.UtcNow <= deadlineUtc) {
                try {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(lockPath, fullProjectRootPath);
                    FileStream lockStream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.SequentialScan);
                    EditorProjectWriteLock acquiredLock = new EditorProjectWriteLock(lockStream, canonicalLockPath, true);
                    (HeldLocks ??= new Dictionary<string, EditorProjectWriteLock>(
                        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))[canonicalLockPath] = acquiredLock;
                    return acquiredLock;
                } catch (IOException exception) {
                    lastIOException = exception;
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }

            throw new IOException($"Could not acquire the project authoring write lock at '{lockPath}'.", lastIOException);
        }

        /// <summary>
        /// Canonicalizes the lock-file identity, including any linked ancestor directory.
        /// </summary>
        /// <param name="lockPath">Absolute lock-file path.</param>
        /// <returns>Canonical lock-file key used for reentrant ownership.</returns>
        static string CanonicalizeLockPath(string lockPath) {
            string fullLockPath = Path.GetFullPath(lockPath);
            string directoryPath = Path.GetDirectoryName(fullLockPath);
            string fileName = Path.GetFileName(fullLockPath);
            List<string> suffix = new List<string>();
            DirectoryInfo current = new DirectoryInfo(directoryPath);
            while (current != null) {
                try {
                    DirectoryInfo resolved = current.ResolveLinkTarget(true) as DirectoryInfo;
                    if (resolved != null) {
                        string canonicalDirectory = resolved.FullName;
                        for (int index = suffix.Count - 1; index >= 0; index--) {
                            canonicalDirectory = Path.Combine(canonicalDirectory, suffix[index]);
                        }
                        return Path.Combine(canonicalDirectory, fileName);
                    }
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                } catch (PlatformNotSupportedException) {
                }

                DirectoryInfo parent = current.Parent;
                if (parent == null) {
                    break;
                }
                suffix.Add(current.Name);
                current = parent;
            }

            return Path.Combine(directoryPath, fileName);
        }

        /// <summary>
        /// Releases the exclusive project lock.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            if (!OwnsHandle) {
                IsDisposed = true;
                return;
            }

            // Remove the ambient ownership only after the underlying handle has
            // closed successfully, so a failed release can be retried safely.
            LockStream.Dispose();
            if (HeldLocks != null && HeldLocks.TryGetValue(ProjectRootPath, out EditorProjectWriteLock heldLock) &&
                ReferenceEquals(heldLock, this)) {
                HeldLocks.Remove(ProjectRootPath);
            }
            IsDisposed = true;
        }
    }

    /// <summary>
    /// Describes one ordered, exact-path authoring publication.
    /// </summary>
    internal sealed class EditorProjectWriteChange {
        public EditorProjectWriteChange(long generation, string relativePath) {
            Generation = generation;
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        }

        public long Generation { get; }

        public string RelativePath { get; }
    }

    /// <summary>
    /// Publishes and reads project-scoped exact-path changes.
    /// </summary>
    internal interface IEditorProjectWriteChangeLog {
        long CurrentGeneration { get; }

        IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation);

        long PublishChange(string relativePath);

        long PublishChanges(IReadOnlyList<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) {
                throw new ArgumentException("At least one changed path is required.", nameof(relativePaths));
            }

            long generation = 0;
            for (int index = 0; index < relativePaths.Count; index++) {
                generation = PublishChange(relativePaths[index]);
            }
            return generation;
        }

        long PublishRollbackChanges(string transactionId, IReadOnlyList<string> relativePaths) {
            return PublishChanges(relativePaths);
        }

        long BeginRepairBatch(IReadOnlyList<string> relativePaths);

        void CommitRepairBatch(long batchId);

        void CancelRepairBatch(long batchId);
    }

    /// <summary>
    /// File-backed exact-path change log used by project authoring sessions.
    /// </summary>
    internal sealed class FileEditorProjectWriteChangeLog : IEditorProjectWriteChangeLog {
        readonly string ProjectRootPath;

        public FileEditorProjectWriteChangeLog(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public long CurrentGeneration => EditorProjectWriteGeneration.Read(ProjectRootPath);

        public IReadOnlyList<EditorProjectWriteChange> ReadAfter(long generation) {
            return EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, generation);
        }

        public long PublishChange(string relativePath) {
            return EditorProjectWriteGeneration.PublishChangeUnderLock(ProjectRootPath, relativePath);
        }

        public long PublishChanges(IReadOnlyList<string> relativePaths) {
            return EditorProjectWriteGeneration.PublishChangesUnderLock(ProjectRootPath, relativePaths);
        }

        public long PublishRollbackChanges(string transactionId, IReadOnlyList<string> relativePaths) {
            return EditorProjectWriteGeneration.PublishRollbackChangesUnderLock(ProjectRootPath, transactionId, relativePaths);
        }

        public long BeginRepairBatch(IReadOnlyList<string> relativePaths) {
            return EditorProjectWriteGeneration.BeginRepairBatchUnderLock(ProjectRootPath, relativePaths);
        }

        public void CommitRepairBatch(long batchId) {
            EditorProjectWriteGeneration.CommitRepairBatchUnderLock(ProjectRootPath, batchId);
        }

        public void CancelRepairBatch(long batchId) {
            EditorProjectWriteGeneration.CancelRepairBatchUnderLock(ProjectRootPath, batchId);
        }
    }

    /// <summary>
    /// Stores ordered exact-path records for project-scoped authoring publication.
    /// </summary>
    internal static class EditorProjectWriteGeneration {
        const int CurrentVersion = 1;
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// Reads the current generation from the strict project snapshot, or zero when no snapshot exists.
        /// </summary>
        public static long Read(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, generationPath);
            return snapshot.CurrentGeneration;
        }

        /// <summary>
        /// Reads latest-per-path changes after one observed generation.
        /// </summary>
        public static IReadOnlyList<EditorProjectWriteChange> ReadAfter(string projectRootPath, long generation) {
            if (generation < 0) {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            string generationPath = GetPath(projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, generationPath);
            return snapshot.Changes
                .Where(change => change.Generation > generation)
                .OrderBy(change => change.Generation)
                .ThenBy(change => change.RelativePath, PathComparer)
                .Select(change => new EditorProjectWriteChange(change.Generation, change.RelativePath))
                .ToArray();
        }

        /// <summary>
        /// Durably publishes one exact normalized path by atomically replacing the generation snapshot.
        /// The caller holds the project write lock for the full read-modify-write boundary.
        /// </summary>
        public static long PublishChange(string projectRootPath, string relativePath) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            return PublishChangeUnderLock(projectRootPath, relativePath);
        }

        /// <summary>
        /// Persists one pending identity-repair batch without publishing ordinary change records.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the snapshot.</param>
        /// <param name="relativePaths">Exact assets-relative paths in the staged batch.</param>
        /// <returns>Opaque batch token used to commit or cancel.</returns>
        public static long BeginRepairBatch(string projectRootPath, IReadOnlyList<string> relativePaths) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            return BeginRepairBatchUnderLock(projectRootPath, relativePaths);
        }

        /// <summary>
        /// Commits one pending identity-repair batch while the caller owns the project lock.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the snapshot.</param>
        /// <param name="batchId">Pending batch token.</param>
        public static void CommitRepairBatch(string projectRootPath, long batchId) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            CommitRepairBatchUnderLock(projectRootPath, batchId);
        }

        /// <summary>
        /// Cancels one pending identity-repair batch while the caller owns the project lock.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the snapshot.</param>
        /// <param name="batchId">Pending batch token.</param>
        public static void CancelRepairBatch(string projectRootPath, long batchId) {
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            CancelRepairBatchUnderLock(projectRootPath, batchId);
        }

        /// <summary>
        /// Publishes one exact path while the caller owns the project publication lock.
        /// </summary>
        internal static long PublishChangeUnderLock(string projectRootPath, string relativePath) {
            return PublishChangesUnderLock(projectRootPath, new[] { relativePath });
        }

        /// <summary>
        /// Publishes a complete changed-path set as one generation snapshot replacement.
        /// </summary>
        internal static long PublishChangesUnderLock(string projectRootPath, IReadOnlyList<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) {
                throw new ArgumentException("At least one changed path is required.", nameof(relativePaths));
            }
            List<string> normalizedPaths = relativePaths
                .Select(relativePath => NormalizeRelativePath(projectRootPath, relativePath))
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList();
            string generationPath = GetPath(projectRootPath);
            string generationDirectoryPath = Path.GetDirectoryName(generationPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationDirectoryPath, projectRootPath);
            Directory.CreateDirectory(generationDirectoryPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationDirectoryPath, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationPath, projectRootPath);

            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, generationPath);
            long generation = checked(snapshot.CurrentGeneration + normalizedPaths.Count);
            Dictionary<string, ProjectWriteGenerationChange> changes = snapshot.Changes
                .ToDictionary(change => change.RelativePath, change => change, PathComparer);
            long nextGeneration = snapshot.CurrentGeneration;
            for (int index = 0; index < normalizedPaths.Count; index++) {
                string normalizedRelativePath = normalizedPaths[index];
                changes[normalizedRelativePath] = new ProjectWriteGenerationChange {
                    Generation = ++nextGeneration,
                    RelativePath = normalizedRelativePath
                };
            }

            ProjectWriteGenerationSnapshot nextSnapshot = new ProjectWriteGenerationSnapshot {
                Version = CurrentVersion,
                CurrentGeneration = nextGeneration,
                Changes = changes.Values
                    .OrderBy(change => change.Generation)
                    .ThenBy(change => change.RelativePath, PathComparer)
                    .ToList(),
                RollbackTransactionIds = snapshot.RollbackTransactionIds
            };
            WriteSnapshotAtomically(generationPath, nextSnapshot);
            return generation;
        }

        /// <summary>
        /// Publishes restored paths for a transaction exactly once. The token
        /// and the path generation are committed in one atomic snapshot.
        /// </summary>
        internal static long PublishRollbackChangesUnderLock(
            string projectRootPath,
            string transactionId,
            IReadOnlyList<string> relativePaths) {
            if (string.IsNullOrWhiteSpace(transactionId) || !Guid.TryParseExact(transactionId, "N", out _)) {
                throw new ArgumentException("A current transaction identifier is required.", nameof(transactionId));
            }
            if (relativePaths == null || relativePaths.Count == 0) {
                throw new ArgumentException("At least one restored path is required.", nameof(relativePaths));
            }

            string generationPath = GetPath(projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, generationPath);
            snapshot.RollbackTransactionIds ??= new List<string>();
            if (snapshot.RollbackTransactionIds.Contains(transactionId, StringComparer.Ordinal)) {
                return snapshot.CurrentGeneration;
            }

            List<string> normalizedPaths = relativePaths
                .Select(relativePath => NormalizeRelativePath(projectRootPath, relativePath))
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (normalizedPaths.Count == 0) {
                throw new ArgumentException("At least one restored path is required.", nameof(relativePaths));
            }

            Dictionary<string, ProjectWriteGenerationChange> changes = snapshot.Changes
                .ToDictionary(change => change.RelativePath, change => change, PathComparer);
            long nextGeneration = snapshot.CurrentGeneration;
            for (int index = 0; index < normalizedPaths.Count; index++) {
                string relativePath = normalizedPaths[index];
                changes[relativePath] = new ProjectWriteGenerationChange {
                    Generation = checked(++nextGeneration),
                    RelativePath = relativePath
                };
            }

            snapshot.CurrentGeneration = nextGeneration;
            snapshot.Changes = changes.Values
                .OrderBy(change => change.Generation)
                .ThenBy(change => change.RelativePath, PathComparer)
                .ToList();
            snapshot.RollbackTransactionIds.Add(transactionId);
            WriteSnapshotAtomically(generationPath, snapshot);
            return nextGeneration;
        }

        internal static bool HasRollbackPublicationUnderLock(string projectRootPath, string transactionId) {
            if (string.IsNullOrWhiteSpace(transactionId) || !Guid.TryParseExact(transactionId, "N", out _)) {
                throw new ArgumentException("A current transaction identifier is required.", nameof(transactionId));
            }

            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, GetPath(projectRootPath));
            return snapshot.RollbackTransactionIds != null &&
                snapshot.RollbackTransactionIds.Contains(transactionId, StringComparer.Ordinal);
        }

        /// <summary>
        /// Persists one pending identity-repair batch while the caller owns the project lock.
        /// </summary>
        internal static long BeginRepairBatchUnderLock(string projectRootPath, IReadOnlyList<string> relativePaths) {
            if (relativePaths == null || relativePaths.Count == 0) {
                throw new ArgumentException("A repair batch must contain at least one path.", nameof(relativePaths));
            }

            string generationPath = GetPath(projectRootPath);
            string generationDirectoryPath = Path.GetDirectoryName(generationPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationDirectoryPath, projectRootPath);
            Directory.CreateDirectory(generationDirectoryPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationDirectoryPath, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationPath, projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            EnsureNoPendingRepair(snapshot, generationPath);
            List<string> normalizedPaths = relativePaths
                .Select(path => NormalizeRelativePath(projectRootPath, path))
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (normalizedPaths.Count == 0) {
                throw new ArgumentException("A repair batch must contain at least one path.", nameof(relativePaths));
            }

            long batchId = checked(snapshot.CurrentGeneration + 1);
            snapshot.PendingRepair = new ProjectWriteGenerationPendingRepair {
                BatchId = batchId,
                RelativePaths = normalizedPaths
            };
            WriteSnapshotAtomically(generationPath, snapshot);
            return batchId;
        }

        /// <summary>
        /// Commits one pending identity-repair batch while the caller owns the project lock.
        /// </summary>
        internal static void CommitRepairBatchUnderLock(string projectRootPath, long batchId) {
            string generationPath = GetPath(projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            ProjectWriteGenerationPendingRepair pendingRepair = RequirePendingRepair(snapshot, generationPath, batchId);
            Dictionary<string, ProjectWriteGenerationChange> changes = snapshot.Changes
                .ToDictionary(change => change.RelativePath, change => change, PathComparer);
            long nextGeneration = snapshot.CurrentGeneration;
            for (int index = 0; index < pendingRepair.RelativePaths.Count; index++) {
                nextGeneration = checked(nextGeneration + 1);
                string relativePath = pendingRepair.RelativePaths[index];
                changes[relativePath] = new ProjectWriteGenerationChange {
                    Generation = nextGeneration,
                    RelativePath = relativePath
                };
            }

            snapshot.CurrentGeneration = nextGeneration;
            snapshot.Changes = changes.Values
                .OrderBy(change => change.Generation)
                .ThenBy(change => change.RelativePath, PathComparer)
                .ToList();
            snapshot.PendingRepair = null;
            WriteSnapshotAtomically(generationPath, snapshot);
        }

        /// <summary>
        /// Cancels one pending identity-repair batch while the caller owns the project lock.
        /// </summary>
        internal static void CancelRepairBatchUnderLock(string projectRootPath, long batchId) {
            string generationPath = GetPath(projectRootPath);
            ProjectWriteGenerationSnapshot snapshot = ReadSnapshot(projectRootPath);
            RequirePendingRepair(snapshot, generationPath, batchId);
            snapshot.PendingRepair = null;
            WriteSnapshotAtomically(generationPath, snapshot);
        }

        static void EnsureNoPendingRepair(ProjectWriteGenerationSnapshot snapshot, string generationPath) {
            if (snapshot?.PendingRepair != null) {
                throw new InvalidOperationException($"Project authoring repair recovery is required for pending batch '{snapshot.PendingRepair.BatchId}' in '{generationPath}'.");
            }
        }

        static ProjectWriteGenerationPendingRepair RequirePendingRepair(ProjectWriteGenerationSnapshot snapshot, string generationPath, long batchId) {
            if (snapshot?.PendingRepair == null || snapshot.PendingRepair.BatchId != batchId) {
                throw new InvalidOperationException($"Project authoring repair batch '{batchId}' is not pending in '{generationPath}'.");
            }
            return snapshot.PendingRepair;
        }

        static ProjectWriteGenerationSnapshot ReadSnapshot(string projectRootPath) {
            string generationPath = GetPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationPath, projectRootPath);
            EditorAuthoringTransactionPendingMarker.EnsureNoPending(projectRootPath);
            if (!File.Exists(generationPath)) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            }

            string json;
            try {
                json = File.ReadAllText(generationPath);
            } catch (FileNotFoundException) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            } catch (DirectoryNotFoundException) {
                return new ProjectWriteGenerationSnapshot {
                    Version = CurrentVersion,
                    CurrentGeneration = 0,
                    Changes = new List<ProjectWriteGenerationChange>()
                };
            }

            ProjectWriteGenerationSnapshot snapshot;
            try {
                snapshot = JsonSerializer.Deserialize<ProjectWriteGenerationSnapshot>(json, JsonOptions);
            } catch (JsonException exception) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' is malformed.", exception);
            }

            ValidateSnapshot(snapshot, generationPath, Path.GetFullPath(projectRootPath));
            return snapshot;
        }

        static void ValidateSnapshot(ProjectWriteGenerationSnapshot snapshot, string generationPath, string projectRootPath) {
            if (snapshot == null || snapshot.Version != CurrentVersion || snapshot.CurrentGeneration < 0 || snapshot.Changes == null) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' has an unsupported version or shape.");
            }

            HashSet<string> rollbackTransactions = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.RollbackTransactionIds != null) {
                for (int index = 0; index < snapshot.RollbackTransactionIds.Count; index++) {
                    string transactionId = snapshot.RollbackTransactionIds[index];
                    if (!Guid.TryParseExact(transactionId, "N", out _) || !rollbackTransactions.Add(transactionId)) {
                        throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid rollback publication token.");
                    }
                }
            }

            HashSet<string> paths = new HashSet<string>(PathComparer);
            HashSet<long> generations = new HashSet<long>();
            long maximumGeneration = 0;
            for (int index = 0; index < snapshot.Changes.Count; index++) {
                ProjectWriteGenerationChange change = snapshot.Changes[index];
                if (change == null || string.IsNullOrWhiteSpace(change.RelativePath) || change.Generation <= 0 || change.Generation > snapshot.CurrentGeneration ||
                    !generations.Add(change.Generation) || !paths.Add(change.RelativePath)) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid or duplicate change.");
                }

                string normalizedPath;
                try {
                    normalizedPath = NormalizeRelativePath(projectRootPath, change.RelativePath);
                } catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid path.", exception);
                }
                if (!string.Equals(normalizedPath, change.RelativePath, StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains a non-canonical path.");
                }

                maximumGeneration = Math.Max(maximumGeneration, change.Generation);
            }

            if ((snapshot.CurrentGeneration == 0 && snapshot.Changes.Count != 0) ||
                (snapshot.CurrentGeneration > 0 && maximumGeneration != snapshot.CurrentGeneration)) {
                throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' has inconsistent generation bounds.");
            }

            if (snapshot.PendingRepair != null) {
                if (snapshot.PendingRepair.BatchId <= snapshot.CurrentGeneration ||
                    snapshot.PendingRepair.RelativePaths == null ||
                    snapshot.PendingRepair.RelativePaths.Count == 0) {
                    throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid pending repair batch.");
                }

                HashSet<string> pendingPaths = new HashSet<string>(PathComparer);
                for (int index = 0; index < snapshot.PendingRepair.RelativePaths.Count; index++) {
                    string relativePath = snapshot.PendingRepair.RelativePaths[index];
                    string normalizedPath;
                    try {
                        normalizedPath = NormalizeRelativePath(projectRootPath, relativePath);
                    } catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException) {
                        throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains an invalid pending path.", exception);
                    }

                    if (!string.Equals(normalizedPath, relativePath, StringComparison.Ordinal) || !pendingPaths.Add(relativePath)) {
                        throw new InvalidDataException($"The project authoring generation snapshot '{generationPath}' contains a duplicate or non-canonical pending path.");
                    }
                }
            }
        }

        static void WriteSnapshotAtomically(string generationPath, ProjectWriteGenerationSnapshot snapshot) {
            string projectRootPath = Path.GetDirectoryName(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(generationPath)));
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationPath, projectRootPath);
            string temporaryPath = generationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporaryPath, projectRootPath);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(generationPath, projectRootPath);
                if (File.Exists(generationPath)) {
                    File.Move(temporaryPath, generationPath, true);
                } else {
                    File.Move(temporaryPath, generationPath);
                }
            } finally {
                if (File.Exists(temporaryPath)) {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporaryPath, projectRootPath);
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>
        /// Gets the generation marker path for one project.
        /// </summary>
        /// <param name="projectRootPath">Project root to inspect.</param>
        /// <returns>Absolute generation marker path.</returns>
        static string GetPath(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "editor", "authoring-write.generation");
        }

        /// <summary>
        /// Validates and normalizes one path beneath the project assets root.
        /// </summary>
        static string NormalizeRelativePath(string projectRootPath, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
                relativePath.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0) {
                throw new ArgumentException("Changed path must be a non-rooted assets-relative file path.", nameof(relativePath));
            }

            string assetsRootPath = Path.GetFullPath(Path.Combine(Path.GetFullPath(projectRootPath), "assets"));
            string fullPath = Path.GetFullPath(Path.Combine(assetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = assetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison)) {
                throw new InvalidOperationException("Changed path must remain beneath the project assets root.");
            }

            return Path.GetRelativePath(assetsRootPath, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Trim('/');
        }

        sealed class ProjectWriteGenerationSnapshot {
            public int Version { get; set; }

            public long CurrentGeneration { get; set; }

            public List<ProjectWriteGenerationChange> Changes { get; set; }

            public ProjectWriteGenerationPendingRepair PendingRepair { get; set; }

            public List<string> RollbackTransactionIds { get; set; }
        }

        sealed class ProjectWriteGenerationChange {
            public long Generation { get; set; }

            public string RelativePath { get; set; }
        }

        sealed class ProjectWriteGenerationPendingRepair {
            public long BatchId { get; set; }

            public List<string> RelativePaths { get; set; }
        }
    }
}
