using System.Text;
using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Records multi-step inode-bound namespace operations in a small current-format
    /// journal. The journal is deliberately project-scoped so startup can fail closed
    /// when a namespace operation stopped between syscalls.
    /// </summary>
    internal sealed class EditorAuthoringMutationJournal : IDisposable {
        const int CurrentVersion = 1;
        const string JournalDirectoryName = "authoring-mutations";
        static readonly HashSet<string> SupportedKinds = new HashSet<string>(StringComparer.Ordinal) {
            "replace",
            "move",
            "move-directory",
            "copy",
            "delete",
            "delete-directory"
        };
        static readonly HashSet<string> SupportedTransientEntryKinds = new HashSet<string>(StringComparer.Ordinal) {
            "File",
            "Directory"
        };
        static readonly HashSet<string> SupportedTransientRecoveryIntents = new HashSet<string>(StringComparer.Ordinal) {
            "RestoreOriginal",
            "RollbackPublication"
        };
        static readonly HashSet<string> SupportedTransientLifecycles = new HashSet<string>(StringComparer.Ordinal) {
            "Reserved",
            "Occupied",
            "Published",
            "CleanupPending"
        };
        static readonly HashSet<string> SupportedTransientResumePhases = new HashSet<string>(StringComparer.Ordinal) {
            "Prepared"
        };
        static readonly AsyncLocal<EditorAuthoringMutationJournal> Current = new AsyncLocal<EditorAuthoringMutationJournal>();
        static readonly AsyncLocal<int> DocumentWriteDepth = new AsyncLocal<int>();
        readonly string ProjectRootPath;
        readonly string JournalPath;
        readonly MutationDocument Document;
        readonly EditorAuthoringMutationJournal PreviousCurrent;
        readonly EditorProjectWriteLock ProjectWriteLock;
        bool Completed;
        int TransientSequence;

        EditorAuthoringMutationJournal(string projectRootPath, string journalPath, MutationDocument document, EditorAuthoringMutationJournal previousCurrent, EditorProjectWriteLock projectWriteLock) {
            ProjectRootPath = projectRootPath;
            JournalPath = journalPath;
            Document = document;
            PreviousCurrent = previousCurrent;
            ProjectWriteLock = projectWriteLock;
        }

        internal static bool IsWritingDocument => DocumentWriteDepth.Value > 0;

        internal static string CurrentProjectRootPath => DurableCurrent?.ProjectRootPath;

        internal static bool IsFixedDocumentArtifactPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            string fullPath = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(fullPath);
            string journalDirectory = Path.GetDirectoryName(parent);
            string operationId = Path.GetFileName(parent);
            string journalName = Path.GetFileName(journalDirectory);
            if (!string.Equals(journalName, JournalDirectoryName, StringComparison.Ordinal) ||
                !Guid.TryParseExact(operationId, "N", out _)) {
                return false;
            }
            string name = Path.GetFileName(fullPath);
            return name is "document.json" or "document.next" or "document.old" or "destination.old";
        }

        // Payload files and their staging directory are owned by the same
        // operation document. They use the fixed artifact state machine too;
        // moving or deleting one must not allocate an outer quarantine entry.
        internal static bool IsFixedOperationArtifactPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            string fullPath = Path.GetFullPath(path);
            string operationDirectory = Path.GetDirectoryName(fullPath);
            string name = Path.GetFileName(fullPath);
            if (string.Equals(name, "staged", StringComparison.Ordinal) &&
                IsAuthoringMutationDirectoryPath(operationDirectory)) {
                return true;
            }
            if (string.Equals(Path.GetFileName(operationDirectory), "staged", StringComparison.Ordinal)) {
                string operationParent = Path.GetDirectoryName(operationDirectory);
                if (IsAuthoringMutationDirectoryPath(operationParent) &&
                    name is "payload" or "payload.next" or "payload.publishing") {
                    return true;
                }
            }
            string deletingName = Path.GetFileName(fullPath);
            if (deletingName.StartsWith(".deleting-", StringComparison.Ordinal)) {
                string operationId = deletingName.Substring(".deleting-".Length);
                int separator = operationId.IndexOf('-');
                if (separator > 0) {
                    operationId = operationId.Substring(0, separator);
                }
                if (Guid.TryParseExact(operationId, "N", out _)) {
                    return true;
                }
            }
            return IsFixedDocumentArtifactPath(fullPath);
        }

        internal static bool IsRecordedTransientPath(string path) {
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null || string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            try {
                string relativePath = NormalizeRelativePath(journal.ProjectRootPath, path);
                StringComparison comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                return journal.Document.TransientEntries.Any(entry =>
                    entry != null && string.Equals(entry.QuarantineRelativePath, relativePath, comparison));
            } catch (InvalidDataException) {
                return false;
            }
        }

        internal static bool IsAuthoringMutationDirectoryPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            string fullPath = Path.GetFullPath(path);
            string journalDirectory = Path.GetDirectoryName(fullPath);
            if (!string.Equals(Path.GetFileName(journalDirectory), JournalDirectoryName, StringComparison.Ordinal)) {
                return false;
            }
            string name = Path.GetFileName(fullPath);
            return Guid.TryParseExact(name, "N", out _) ||
                name.StartsWith(".creating-", StringComparison.Ordinal) ||
                name.StartsWith(".deleting-", StringComparison.Ordinal);
        }

        internal static IDisposable EnterDocumentWriteScope() {
            DocumentWriteDepth.Value++;
            return new DocumentWriteScope();
        }

        // Recovery runs while the project lock is held, but its fixed
        // primitives still need the parsed operation as their durable owner.
        // Installing that context prevents a recovery quarantine from ever
        // falling back to an unrecorded, process-local name.
        static IDisposable EnterRecovered(string projectRootPath, string journalPath, MutationDocument document) {
            EditorAuthoringMutationJournal recovered = new EditorAuthoringMutationJournal(
                projectRootPath,
                journalPath,
                document,
                Current.Value,
                projectWriteLock: null);
            Current.Value = recovered;
            return new RecoveredScope(recovered);
        }

        sealed class RecoveredScope : IDisposable {
            readonly EditorAuthoringMutationJournal operation;
            bool disposed;

            public RecoveredScope(EditorAuthoringMutationJournal operation) {
                this.operation = operation;
            }

            public void Dispose() {
                if (disposed) {
                    return;
                }
                disposed = true;
                if (ReferenceEquals(Current.Value, operation)) {
                    Current.Value = operation.PreviousCurrent;
                }
            }
        }

        sealed class DocumentWriteScope : IDisposable {
            bool disposed;

            public void Dispose() {
                if (disposed) {
                    return;
                }
                disposed = true;
                DocumentWriteDepth.Value = Math.Max(0, DocumentWriteDepth.Value - 1);
            }
        }

        internal string OperationDirectoryPath => Path.GetDirectoryName(JournalPath);

        internal string ExpectedSourceIdentityValue => Document.ExpectedSourceIdentity;

        internal string ExpectedDestinationIdentityValue => Document.ExpectedDestinationIdentity;

        internal string ExpectedDestinationHashValue => Document.ExpectedDestinationHash;

        internal string StagedIdentityValue => Document.StagedIdentity;

        internal string PublishingPayloadIdentityValue => Document.PublishingPayloadIdentity;

        internal string PublishingPayloadHashValue => Document.PublishingPayloadExactHash;

        internal string DestinationOldIdentityValue => Document.DestinationOldIdentity;

        internal string DestinationOldHashValue => Document.DestinationOldHash;

        internal string CreatePublishingPayloadPath() {
            EnsureOpen();
            EnsureMutationCallbackAllowed();
            string stagedDirectory = Path.Combine(OperationDirectoryPath, "staged");
            EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, stagedDirectory);
            string path = Path.Combine(stagedDirectory, "payload.publishing");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, OperationDirectoryPath);
            Document.PublishingPayloadRelativePath = Path.Combine("staged", "payload.publishing").Replace(Path.DirectorySeparatorChar, '/');
            // Staged identity/hash are already durably recorded, so bind the
            // fixed publishing name before moving the inode into it.
            Document.PublishingPayloadIdentity = Document.StagedIdentity;
            Document.PublishingPayloadExactHash = Document.StagedExactHash;
            Persist();
            return path;
        }

        internal string CreateDestinationOldPath() {
            EnsureOpen();
            EnsureMutationCallbackAllowed();
            string path = Path.Combine(OperationDirectoryPath, "destination.old");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, OperationDirectoryPath);
            Document.DestinationOldRelativePath = "destination.old";
            // The destination proof is captured at Begin and is the durable
            // identity expected at the fixed former-destination name. Store
            // it before the namespace move so a crash between rename and the
            // next document update remains recoverable.
            Document.DestinationOldIdentity = Document.ExpectedDestinationIdentity;
            Document.DestinationOldHash = Document.ExpectedDestinationHash;
            Persist();
            return path;
        }

        internal void RecordPublishingPayload(string path) {
            EnsureOpen();
            EnsureMutationCallbackAllowed();
            string fullPath = RequireContainedArtifact(path, "staged/payload.publishing");
            string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, fullPath);
            string hash = EditorAuthoringMutationScope.TryGetVerifiedSha256(ProjectRootPath, fullPath);
            if (identity == "missing" || identity == "unavailable" || hash == "missing" || hash == "unavailable") {
                throw new InvalidDataException("The publishing payload identity could not be verified.");
            }
            if (!string.Equals(identity, Document.StagedIdentity, StringComparison.Ordinal) ||
                !string.Equals(hash, Document.StagedExactHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The publishing payload does not match the staged payload proof.");
            }
            Document.PublishingPayloadIdentity = identity;
            Document.PublishingPayloadExactHash = hash;
            Document.Phase = "PayloadPublishing";
            Persist();
        }

        internal void RecordDestinationOld(string path) {
            EnsureOpen();
            EnsureMutationCallbackAllowed();
            string fullPath = RequireContainedArtifact(path, "destination.old");
            string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, fullPath);
            string hash = EditorAuthoringMutationScope.TryGetVerifiedSha256(ProjectRootPath, fullPath);
            if (identity == "missing" || identity == "unavailable" || hash == "missing" || hash == "unavailable") {
                throw new InvalidDataException("The former destination identity could not be verified.");
            }
            if (!string.Equals(identity, Document.ExpectedDestinationIdentity, StringComparison.Ordinal) ||
                !string.Equals(hash, Document.ExpectedDestinationHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The former destination does not match the destination proof.");
            }
            Document.DestinationOldIdentity = identity;
            Document.DestinationOldHash = hash;
            Document.Phase = "DestinationQuarantined";
            Persist();
        }

        internal void ValidatePublishedPayload(string destinationPath) {
            EnsureOpen();
            string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destinationPath);
            string hash = EditorAuthoringMutationScope.TryGetVerifiedSha256(ProjectRootPath, destinationPath);
            if (!string.Equals(identity, Document.StagedIdentity, StringComparison.Ordinal) ||
                !string.Equals(hash, Document.StagedExactHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The published destination does not match the staged payload proof.");
            }
        }

        void EnsureOpen() {
            if (Completed) {
                throw new InvalidOperationException("The authoring mutation journal is already complete.");
            }
        }

        static void EnsureMutationCallbackAllowed() {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Journal mutation callbacks cannot run while a document is being written.");
            }
        }

        string RequireContainedArtifact(string path, string expectedRelativePath) {
            string fullPath = Path.GetFullPath(path);
            string expected = Path.GetFullPath(Path.Combine(OperationDirectoryPath, expectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(fullPath, expected, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                throw new InvalidDataException("The authoring mutation artifact path was not the fixed operation-owned path.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, OperationDirectoryPath);
            return fullPath;
        }

        internal string RequireDestinationIdentity(string destinationPath) {
            string actual = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destinationPath);
            if (!string.Equals(actual, Document.ExpectedDestinationIdentity, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation destination '{destinationPath}' changed after journal creation.");
            }
            string actualHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(ProjectRootPath, destinationPath);
            if (!string.Equals(actualHash, Document.ExpectedDestinationHash, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation destination '{destinationPath}' content changed after journal creation.");
            }
            return actual;
        }

        internal string CreateDeletingPath(string originalPath) {
            EnsureMutationCallbackAllowed();
            if (Completed) {
                throw new InvalidOperationException("The authoring mutation journal is already complete.");
            }
            string original = Path.GetFullPath(originalPath);
            string parent = Path.GetDirectoryName(original);
            string deletingPath = Path.Combine(parent, ".deleting-" + Document.OperationId + "-" + Path.GetFileName(original));
            Document.DestinationRelativePath = NormalizeRelativePath(ProjectRootPath, deletingPath);
            Persist();
            return deletingPath;
        }

        internal string CreateStagedPayloadPath(string fileName) {
            EnsureMutationCallbackAllowed();
            if (Completed) {
                throw new InvalidOperationException("The authoring mutation journal is already complete.");
            }
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 ||
                fileName == "." || fileName == "..") {
                throw new InvalidDataException("The staged authoring payload must be one contained file name.");
            }
            string stagedDirectory = Path.Combine(OperationDirectoryPath, "staged");
            EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, stagedDirectory);
            string stagedPath = Path.Combine(stagedDirectory, fileName);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(stagedPath, OperationDirectoryPath);
            Document.StagedRelativePath = Path.Combine("staged", fileName).Replace(Path.DirectorySeparatorChar, '/');
            return stagedPath;
        }

        /// <summary>
        /// Returns the fixed staging write name. Callers write and flush this
        /// leaf first, then promote it to <c>payload</c> with the fixed
        /// no-replace primitive. Keeping the incomplete write at a recognized
        /// name lets recovery discard it without probing arbitrary siblings.
        /// </summary>
        internal string CreateStagedPayloadNextPath() {
            EnsureMutationCallbackAllowed();
            if (Completed) {
                throw new InvalidOperationException("The authoring mutation journal is already complete.");
            }
            string stagedDirectory = Path.Combine(OperationDirectoryPath, "staged");
            EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, stagedDirectory);
            string stagedPath = Path.Combine(stagedDirectory, "payload");
            string nextPath = stagedPath + ".next";
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(nextPath, OperationDirectoryPath);
            Document.Phase = "StagingAllocated";
            Persist();
            return nextPath;
        }

        internal void RecordStagedPayload(string stagedPath, string exactHash) {
            EnsureMutationCallbackAllowed();
            if (string.IsNullOrWhiteSpace(exactHash)) {
                throw new ArgumentException("The staged payload hash must be provided.", nameof(exactHash));
            }
            string fullPath = Path.GetFullPath(stagedPath);
            string stagedPrefix = Path.Combine(OperationDirectoryPath, "staged") + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(stagedPrefix, comparison) || Path.GetDirectoryName(fullPath).Equals(OperationDirectoryPath, comparison)) {
                throw new InvalidDataException("The staged authoring payload escaped its operation staging directory.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, OperationDirectoryPath);
            Document.StagedRelativePath = Path.GetRelativePath(OperationDirectoryPath, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            Document.StagedExactHash = exactHash;
            Document.StagedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, fullPath);
            if (Document.StagedIdentity == "missing" || Document.StagedIdentity == "unavailable") {
                throw new InvalidDataException("The staged authoring payload identity could not be verified.");
            }
            Document.Phase = "Staged";
            Persist();
        }

        internal void ValidateStagedPayload() {
            if (string.IsNullOrWhiteSpace(Document.StagedRelativePath) || string.IsNullOrWhiteSpace(Document.StagedExactHash) ||
                string.IsNullOrWhiteSpace(Document.StagedIdentity)) {
                throw new InvalidDataException("The authoring mutation has no staged payload proof.");
            }
            string stagedPath = Path.Combine(
                OperationDirectoryPath,
                Document.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string actualHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(ProjectRootPath, stagedPath);
            if (!string.Equals(actualHash, Document.StagedExactHash, StringComparison.Ordinal)) {
                throw new InvalidDataException("The staged authoring payload changed before publication.");
            }
            string actualIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, stagedPath);
            if (!string.Equals(actualIdentity, Document.StagedIdentity, StringComparison.Ordinal)) {
                throw new InvalidDataException("The staged authoring payload identity changed before publication.");
            }
        }

        internal static EditorAuthoringMutationJournal Begin(string projectRootPath, string kind, string sourcePath, string destinationPath) {
            EnsureMutationCallbackAllowed();
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(kind) || !SupportedKinds.Contains(kind)) {
                throw new ArgumentException($"Unsupported authoring mutation kind '{kind}'.", nameof(kind));
            }
            string root = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(root, root);
            EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(root);
            string creatingDirectory = null;
            string operationDirectory = null;
            try {
                string sourceRelativePath = NormalizeRelativePath(root, sourcePath);
                string destinationRelativePath = NormalizeRelativePath(root, destinationPath);
                string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
                EditorAuthoringMutationScope.EnsureDirectory(root, journalDirectory);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalDirectory, root);
                string operationId = Guid.NewGuid().ToString("N");
                operationDirectory = Path.Combine(journalDirectory, operationId);
                creatingDirectory = Path.Combine(journalDirectory, ".creating-" + operationId);
                EditorAuthoringMutationScope.EnsureDirectory(root, creatingDirectory);
                string journalPath = Path.Combine(creatingDirectory, "document.json");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalPath, creatingDirectory);
                MutationDocument document = new MutationDocument {
                    Version = CurrentVersion,
                    OperationId = operationId,
                    Kind = kind ?? string.Empty,
                    SourceRelativePath = sourceRelativePath,
                    DestinationRelativePath = destinationRelativePath,
                    ExpectedSourceIdentity = CaptureIdentity(root, sourcePath),
                    ExpectedSourceHash = CaptureHash(root, sourcePath),
                    ExpectedDestinationIdentity = CaptureIdentity(root, destinationPath),
                    ExpectedDestinationHash = CaptureHash(root, destinationPath),
                    Phase = "Prepared",
                    TransientEntries = new List<TransientMutation>()
                };
                try {
                    WriteDocument(journalPath, document, root, createNew: true);
                } catch (Exception primaryException) {
                    try {
                        string cleanupDirectory = Directory.Exists(operationDirectory)
                            ? operationDirectory
                            : creatingDirectory;
                        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, cleanupDirectory, journalDirectory);
                    } catch (Exception cleanupException) {
                        throw new AggregateException("Authoring mutation journal construction failed and cleanup failed.", primaryException, cleanupException);
                    }
                    throw;
                }
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, creatingDirectory, operationDirectory);
                journalPath = Path.Combine(operationDirectory, "document.json");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalPath, operationDirectory);
                EditorAuthoringMutationJournal journal = new EditorAuthoringMutationJournal(root, journalPath, document, Current.Value, projectWriteLock);
                Current.Value = journal;
                projectWriteLock = null;
                return journal;
            } catch (Exception primaryException) {
                List<Exception> cleanupFailures = new List<Exception>();
                try {
                    if (!string.IsNullOrWhiteSpace(operationDirectory) && Directory.Exists(operationDirectory)) {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, operationDirectory, Path.GetDirectoryName(operationDirectory));
                    }
                } catch (Exception cleanupException) {
                    cleanupFailures.Add(cleanupException);
                }
                try {
                    if (!string.IsNullOrWhiteSpace(creatingDirectory) && Directory.Exists(creatingDirectory)) {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, creatingDirectory, Path.GetDirectoryName(creatingDirectory));
                    }
                } catch (Exception cleanupException) {
                    cleanupFailures.Add(cleanupException);
                }
                try {
                    projectWriteLock?.Dispose();
                } catch (Exception cleanupException) {
                    cleanupFailures.Add(cleanupException);
                }
                if (cleanupFailures.Count > 0) {
                    cleanupFailures.Insert(0, primaryException);
                    throw new AggregateException("Authoring mutation journal construction failed and cleanup failed.", cleanupFailures);
                }
                throw;
            }
        }

        internal static string ReserveTransientName(string originalName) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Document persistence cannot reserve a transient mutation name while its document is being written.");
            }
            throw new InvalidOperationException("A transient mutation name requires its durable project-owned parent and identity proof.");
        }

        internal static string ReserveTransientName(
            string originalName,
            string parentPath,
            string expectedIdentity,
            string expectedHash,
            string action) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Document persistence cannot reserve a transient mutation name while its document is being written.");
            }
            if (string.IsNullOrWhiteSpace(originalName) || Path.IsPathRooted(originalName) ||
                originalName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 ||
                originalName is "." or "..") {
                throw new ArgumentException("A transient mutation requires one original leaf name.", nameof(originalName));
            }
            bool isDirectory = string.Equals(expectedHash, "directory", StringComparison.Ordinal) ||
                expectedIdentity.EndsWith(":directory", StringComparison.Ordinal) ||
                expectedIdentity.EndsWith(";directory", StringComparison.Ordinal);
            string recoveryIntent = action switch {
                "RestoreOriginal" => "RestoreOriginal",
                "RollbackPublication" => "RollbackPublication",
                _ => throw new ArgumentException($"Unsupported transient recovery intent '{action}'.", nameof(action))
            };
            return ReserveTransient(
                Path.Combine(parentPath, originalName),
                parentPath,
                intendedDestinationPath: null,
                expectedIdentity,
                isDirectory ? null : expectedHash,
                isDirectory ? "Directory" : "File",
                recoveryIntent);
        }

        internal static string ReserveTransient(
            string originalPath,
            string parentPath,
            string intendedDestinationPath,
            string expectedIdentity,
            string expectedHash,
            string entryKind,
            string recoveryIntent) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Document persistence cannot reserve a transient mutation name while its document is being written.");
            }
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null) {
                throw new InvalidOperationException("A transient mutation name requires a durable project journal.");
            }
            if (string.IsNullOrWhiteSpace(originalPath) || string.IsNullOrWhiteSpace(parentPath) ||
                string.IsNullOrWhiteSpace(expectedIdentity) || string.IsNullOrWhiteSpace(entryKind) ||
                string.IsNullOrWhiteSpace(recoveryIntent) || !SupportedTransientEntryKinds.Contains(entryKind) ||
                !SupportedTransientRecoveryIntents.Contains(recoveryIntent) ||
                (!string.Equals(journal.Document.Kind, "move", StringComparison.Ordinal) &&
                 !string.Equals(journal.Document.Kind, "replace", StringComparison.Ordinal))) {
                throw new ArgumentException("A durable transient mutation requires valid paths, identity, entry kind, and recovery intent.");
            }
            bool isDirectory = string.Equals(entryKind, "Directory", StringComparison.Ordinal);
            if (isDirectory ? !string.IsNullOrWhiteSpace(expectedHash) : string.IsNullOrWhiteSpace(expectedHash)) {
                throw new ArgumentException(isDirectory
                    ? "Directory transients cannot carry a content hash."
                    : "File transients require a content hash.");
            }
            string originalRelativePath = NormalizeRelativePath(journal.ProjectRootPath, originalPath);
            string parentRelativePath = NormalizeRelativePath(journal.ProjectRootPath, parentPath);
            string normalizedIntendedDestinationPath = string.IsNullOrWhiteSpace(intendedDestinationPath)
                ? null
                : NormalizeRelativePath(journal.ProjectRootPath, intendedDestinationPath);
            if (string.Equals(recoveryIntent, "RestoreOriginal", StringComparison.Ordinal) &&
                normalizedIntendedDestinationPath != null) {
                throw new ArgumentException("A RestoreOriginal transient cannot publish an intended destination.", nameof(intendedDestinationPath));
            }
            if (string.Equals(recoveryIntent, "RollbackPublication", StringComparison.Ordinal) &&
                normalizedIntendedDestinationPath == null) {
                throw new ArgumentException("A RollbackPublication transient requires an intended destination.", nameof(intendedDestinationPath));
            }
            string safeName = Path.GetFileName(originalPath);
            if (string.IsNullOrWhiteSpace(safeName) || safeName is "." or "..") {
                throw new ArgumentException("A transient mutation requires a leaf original path.", nameof(originalPath));
            }
            string transientName = ".authoring-mutation-" + journal.Document.OperationId + "-" + journal.TransientSequence++.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + safeName;
            string quarantineRelativePath = NormalizeRelativePath(journal.ProjectRootPath, Path.Combine(parentPath, transientName));
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (journal.Document.TransientEntries.Any(entry => entry != null &&
                (string.Equals(entry.OriginalRelativePath, originalRelativePath, comparison) ||
                 string.Equals(entry.QuarantineRelativePath, originalRelativePath, comparison) ||
                 string.Equals(entry.QuarantineRelativePath, quarantineRelativePath, comparison) ||
                 string.Equals(entry.OriginalRelativePath, quarantineRelativePath, comparison) ||
                 string.Equals(entry.IntendedDestinationRelativePath, quarantineRelativePath, comparison) ||
                 (!string.IsNullOrWhiteSpace(normalizedIntendedDestinationPath) &&
                  (string.Equals(entry.QuarantineRelativePath, normalizedIntendedDestinationPath, comparison) ||
                   string.Equals(entry.IntendedDestinationRelativePath, normalizedIntendedDestinationPath, comparison)))))) {
                throw new InvalidDataException("The authoring mutation contains a duplicate transient path.");
            }
            journal.Document.TransientEntries.Add(new TransientMutation {
                OriginalRelativePath = originalRelativePath,
                QuarantineRelativePath = quarantineRelativePath,
                IntendedDestinationRelativePath = normalizedIntendedDestinationPath,
                EntryKind = entryKind,
                ExpectedIdentity = expectedIdentity,
                ExpectedHash = expectedHash,
                RecoveryIntent = recoveryIntent,
                Lifecycle = "Reserved"
            });
            if (journal.Document.TransientEntries.Count == 1) {
                journal.Document.ResumePhase = journal.Document.Phase;
                journal.Document.Phase = "Quarantining";
            }
            journal.Persist();
            return transientName;
        }

        internal static void RecordTransientOccupied(string quarantinePath) {
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null) {
                throw new InvalidOperationException("A transient mutation requires a durable project journal.");
            }
            journal.SetTransientLifecycle(quarantinePath, "Occupied");
        }

        internal static void CompleteTransient(string quarantinePath) {
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null) {
                return;
            }
            journal.RemoveTransient(quarantinePath);
        }

        internal static void MarkTransientPublished(string quarantinePath) {
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null) {
                throw new InvalidOperationException("A transient mutation requires a durable project journal.");
            }
            journal.SetTransientLifecycle(quarantinePath, "Published");
        }

        void SetTransientLifecycle(string quarantinePath, string lifecycle) {
            EnsureMutationCallbackAllowed();
            if (!SupportedTransientLifecycles.Contains(lifecycle)) {
                throw new ArgumentException($"Unsupported transient lifecycle '{lifecycle}'.", nameof(lifecycle));
            }
            TransientMutation transient = FindTransient(quarantinePath);
            if (string.Equals(lifecycle, "CleanupPending", StringComparison.Ordinal) &&
                !string.Equals(transient.RecoveryIntent, "RestoreOriginal", StringComparison.Ordinal)) {
                throw new InvalidDataException("Only a former destination can enter cleanup-pending state.");
            }
            transient.Lifecycle = lifecycle;
            Persist();
        }

        void RemoveTransient(string quarantinePath) {
            EnsureMutationCallbackAllowed();
            string relative = NormalizeRelativePath(ProjectRootPath, quarantinePath);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            TransientMutation transient = Document.TransientEntries.FirstOrDefault(entry => entry != null &&
                string.Equals(entry.QuarantineRelativePath, relative, comparison));
            // Completion is intentionally idempotent: a successful fixed
            // deletion may have completed the record before recovery reaches
            // its own bookkeeping step.
            if (transient == null) {
                return;
            }
            int index = Document.TransientEntries.IndexOf(transient);
            Document.TransientEntries.RemoveAt(index);
            if (Document.TransientEntries.Count == 0 &&
                string.Equals(Document.Phase, "Quarantining", StringComparison.Ordinal)) {
                Document.Phase = Document.ResumePhase ?? "Prepared";
                Document.ResumePhase = null;
            }
            try {
                Persist();
            } catch {
                Document.TransientEntries.Insert(index, transient);
                if (Document.TransientEntries.Count == 1 &&
                    string.Equals(Document.Phase, "Prepared", StringComparison.Ordinal)) {
                    Document.Phase = "Quarantining";
                    Document.ResumePhase = "Prepared";
                }
                throw;
            }
        }

        TransientMutation FindTransient(string quarantinePath) {
            string relative = NormalizeRelativePath(ProjectRootPath, quarantinePath);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            TransientMutation transient = Document.TransientEntries.FirstOrDefault(entry => entry != null &&
                string.Equals(entry.QuarantineRelativePath, relative, comparison));
            return transient ?? throw new InvalidDataException($"The transient mutation '{quarantinePath}' is not recorded by this operation.");
        }

        internal void MarkPhase(string phase) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("A journal phase cannot be changed while its document is being written.");
            }
            if (Completed) {
                return;
            }
            Document.Phase = phase ?? throw new ArgumentNullException(nameof(phase));
            if (string.Equals(phase, "Published", StringComparison.Ordinal)) {
                Document.ResumePhase = null;
            }
            Persist();
        }

        internal static void MarkCurrentPhase(string phase) {
            DurableCurrent?.MarkPhase(phase);
        }

        internal static void SetCurrentExpectedIdentities(string sourceIdentity, string destinationIdentity = null) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Journal identity proofs cannot be changed while a document is being written.");
            }
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null || journal.Completed) {
                return;
            }
            journal.Document.ExpectedSourceIdentity = sourceIdentity ?? "unknown";
            if (destinationIdentity != null) {
                journal.Document.ExpectedDestinationIdentity = destinationIdentity;
            }
            journal.Persist();
        }

        static EditorAuthoringMutationJournal DurableCurrent {
            get => Current.Value;
        }

        internal void Complete() {
            EnsureMutationCallbackAllowed();
            if (Completed) {
                return;
            }
            try {
                if (Document.TransientEntries.Count > 0 &&
                    !string.Equals(Document.Phase, "Published", StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        "A journal with live transient entries must publish its outer operation before completion.");
                }
                if (string.Equals(Document.Phase, "Published", StringComparison.Ordinal) &&
                    Document.TransientEntries.Count > 0) {
                    FinalizePublishedTransients();
                }
                Document.Phase = "Completed";
                Persist();
                Completed = true;
                if (ReferenceEquals(Current.Value, this)) {
                    Current.Value = PreviousCurrent;
                }
                // The completed document is a recoverable cleanup marker. A failed
                // retirement must not make a successful namespace mutation appear
                // unsuccessful or re-enter this journal while deleting itself.
                try {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(JournalPath, ProjectRootPath);
                    RetireDocument(ProjectRootPath, JournalPath);
                } catch {
                    // Startup recovery removes completed documents after validating
                    // their contained journal path.
                }
            } finally {
                ProjectWriteLock?.Dispose();
            }
        }

        void FinalizePublishedTransients() {
            foreach (TransientMutation transient in Document.TransientEntries.ToArray()) {
                string quarantinePath = Path.Combine(ProjectRootPath, transient.QuarantineRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string quarantineIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, quarantinePath);
                if (string.Equals(transient.RecoveryIntent, "RollbackPublication", StringComparison.Ordinal)) {
                    if (quarantineIdentity != "missing") {
                        throw new InvalidDataException($"The committed publication quarantine '{quarantinePath}' was not published.");
                    }
                    string intendedPath = Path.Combine(ProjectRootPath, transient.IntendedDestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    VerifyTransientLocation(ProjectRootPath, intendedPath, transient, "published destination");
                    RemoveTransient(quarantinePath);
                    continue;
                }

                if (!string.Equals(transient.RecoveryIntent, "RestoreOriginal", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The committed authoring mutation contains an unsupported cleanup intent.");
                }
                VerifyCurrentPublicationDestination(ProjectRootPath, Document);
                if (!string.Equals(transient.Lifecycle, "CleanupPending", StringComparison.Ordinal)) {
                    transient.Lifecycle = "CleanupPending";
                    Persist();
                }
                if (quarantineIdentity != "missing") {
                    VerifyTransientLocation(ProjectRootPath, quarantinePath, transient, "cleanup quarantine");
                    if (transient.EntryKind == "Directory") {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                            ProjectRootPath,
                            quarantinePath,
                            Path.GetDirectoryName(quarantinePath),
                            transient.ExpectedIdentity);
                    } else {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(
                            ProjectRootPath,
                            quarantinePath,
                            transient.ExpectedIdentity,
                            transient.ExpectedHash);
                    }
                }
                EditorAuthoringMutationScope.FlushContainingDirectoryForRecovery(
                    ProjectRootPath,
                    quarantinePath,
                    "TransientCleanup.FlushParent");
                RemoveTransient(quarantinePath);
            }
        }

        static void VerifyCurrentPublicationDestination(string root, MutationDocument document) {
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
            if (!string.Equals(destinationIdentity, document.ExpectedSourceIdentity, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The published authoring mutation found a changed destination '{destinationPath}'.");
            }
            string destinationHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
            if (!string.Equals(destinationHash, document.ExpectedSourceHash, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The published authoring mutation found changed destination content '{destinationPath}'.");
            }
        }

        internal static void Recover(string projectRootPath) {
            string root = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(root, root);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(root);
            string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
            if (!Directory.Exists(journalDirectory)) {
                return;
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalDirectory, root);
            string[] operationEntries = Directory.GetFileSystemEntries(journalDirectory, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(operationEntries, StringComparer.Ordinal);
            foreach (string operationDirectory in operationEntries) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(operationDirectory, journalDirectory);
                string operationName = Path.GetFileName(operationDirectory);
                if (operationName.StartsWith(".creating-", StringComparison.Ordinal)) {
                    ValidateTransitionDirectoryName(operationName, ".creating-");
                    EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(operationDirectory, journalDirectory);
                    EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, operationDirectory, journalDirectory);
                    continue;
                }
                if (operationName.StartsWith(".deleting-", StringComparison.Ordinal)) {
                    ValidateTransitionDirectoryName(operationName, ".deleting-");
                    EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(operationDirectory, journalDirectory);
                    EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, operationDirectory, journalDirectory);
                    continue;
                }
                if (!Directory.Exists(operationDirectory) || !Guid.TryParseExact(operationName, "N", out _)) {
                    throw new InvalidDataException($"The authoring mutation root contains an unexpected entry '{operationDirectory}'.");
                }
                string path = Path.Combine(operationDirectory, "document.json");
                string nextPath = Path.Combine(operationDirectory, "document.next");
                string oldPath = Path.Combine(operationDirectory, "document.old");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, operationDirectory);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(nextPath, operationDirectory);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(oldPath, operationDirectory);
                bool hasDocument = EntryExists(root, path);
                bool hasNextDocument = EntryExists(root, nextPath);
                bool hasOldDocument = EntryExists(root, oldPath);
                if (!hasDocument && !hasNextDocument) {
                    throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' has no current or next document.");
                }
                MutationDocument currentDocument = null;
                MutationDocument nextDocument = null;
                Exception nextDocumentFailure = null;
                if (hasNextDocument) {
                    try {
                        nextDocument = ReadDocument(nextPath, root);
                        ValidateDocument(nextDocument, nextPath, root, allowNextDocument: true);
                    } catch (Exception exception) {
                        nextDocumentFailure = exception;
                    }
                }
                if (hasDocument) {
                    try {
                        currentDocument = ReadDocument(path, root);
                        ValidateDocument(currentDocument, path, root, allowNextDocument: false);
                    } catch (Exception exception) {
                        if (nextDocumentFailure != null || nextDocument == null) {
                            throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' contains no valid current document.", exception);
                        }
                        throw;
                    }
                }
                if (nextDocumentFailure != null) {
                    // A torn next document is safe to discard only after the
                    // durable current document has been validated. An old
                    // document alongside a torn next cannot identify which
                    // state was intended, so fail closed.
                    if (!hasDocument || hasOldDocument) {
                        throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' contains no recoverable next document.", nextDocumentFailure);
                    }
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
                    hasNextDocument = false;
                } else if (nextDocument != null && (currentDocument == null || nextDocument.Sequence > currentDocument.Sequence)) {
                    if (currentDocument != null) {
                        string currentIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
                        string currentHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, path);
                        RequireDocumentOldProof(nextDocument, operationDirectory);
                        if (!hasOldDocument) {
                            EditorAuthoringMutationScope.FixedRenameNoReplace(
                                root,
                                path,
                                oldPath,
                                currentIdentity,
                                "missing",
                                currentHash);
                            hasOldDocument = true;
                        }
                        RequireExactDocumentArtifact(root, oldPath, nextDocument.DocumentOldIdentity, nextDocument.DocumentOldHash);
                    } else if (!hasOldDocument) {
                        throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' lost its previous document proof.");
                    } else {
                        RequireDocumentOldProof(nextDocument, operationDirectory);
                        RequireExactDocumentArtifact(root, oldPath, nextDocument.DocumentOldIdentity, nextDocument.DocumentOldHash);
                    }

                    string nextIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, nextPath);
                    string nextHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, nextPath);
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, nextPath, path, nextIdentity, "missing", nextHash);
                    hasDocument = true;
                    currentDocument = nextDocument;
                } else if (nextDocument != null) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
                    hasNextDocument = false;
                }
                MutationDocument document;
                try {
                    document = ReadDocument(path, root);
                } catch (Exception exception) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' is malformed.", exception);
                }
                ValidateDocument(document, path, root, allowNextDocument: false);
                using IDisposable recoveredContext = EnterRecovered(root, path, document);
                ValidateOperationEntries(root, operationDirectory, document);
                if (hasOldDocument || EntryExists(root, oldPath)) {
                    RequireDocumentOldProof(document, operationDirectory);
                    RequireExactDocumentArtifact(root, oldPath, document.DocumentOldIdentity, document.DocumentOldHash);
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, oldPath, document.DocumentOldIdentity, document.DocumentOldHash);
                }
                // Transient records are the authoritative recovery graph for
                // inode-bound quarantine operations. Replay them before any
                // source/destination assumptions so a replacement can restore
                // the former destination and source in reverse order.
                if (document.TransientEntries.Count > 0) {
                    TryRecoverTransientEntries(root, path, document);
                    continue;
                }
                if (string.Equals(document.Phase, "Prepared", StringComparison.Ordinal)) {
                    if (document.Kind.StartsWith("delete", StringComparison.Ordinal) &&
                        TryRecoverDeleteBeforePublished(root, path, document)) {
                        continue;
                    }
                    RetireDocument(root, path);
                    continue;
                }
                if (string.Equals(document.Phase, "StagingAllocated", StringComparison.Ordinal)) {
                    string stagedDirectory = Path.Combine(operationDirectory, "staged");
                    if (Directory.Exists(stagedDirectory)) {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, stagedDirectory, operationDirectory);
                    }
                    RetireDocument(root, path);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath) ||
                    !string.IsNullOrWhiteSpace(document.DestinationOldRelativePath)) {
                    RecoverPayloadPublication(root, path, document);
                    continue;
                }
                if (string.Equals(document.Phase, "Staged", StringComparison.Ordinal)) {
                    RecoverBareStagedPayload(root, path, document);
                    continue;
                }
                if (string.Equals(document.Phase, "Publishing", StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The authoring mutation '{path}' has no fixed publication proof.");
                }
                if (string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The authoring mutation '{path}' contains no recoverable transient state.");
                }
                if (string.Equals(document.Phase, "Completed", StringComparison.Ordinal)) {
                    RetireDocument(root, path);
                    continue;
                }
                if (string.Equals(document.Phase, "Published", StringComparison.Ordinal)) {
                    RecoverPublishedDocument(root, path, document);
                    continue;
                }
                throw new InvalidOperationException($"The authoring mutation journal '{path}' is unresolved; startup is blocked until it is repaired.");
            }
        }

        static bool EntryExists(string root, string path) {
            string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
            if (identity == "unavailable") {
                throw new InvalidDataException($"Could not verify authoring mutation entry '{path}'.");
            }
            return identity != "missing";
        }

        static void RequireDocumentOldProof(MutationDocument document, string operationDirectory) {
            if (document == null ||
                !string.Equals(document.DocumentOldRelativePath, "document.old", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(document.DocumentOldIdentity) ||
                string.IsNullOrWhiteSpace(document.DocumentOldHash)) {
                throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' is missing its previous document proof.");
            }
        }

        static void RequireExactDocumentArtifact(string root, string path, string expectedIdentity, string expectedHash) {
            string actualIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
            string actualHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, path);
            if (!string.Equals(actualIdentity, expectedIdentity, StringComparison.Ordinal) ||
                !string.Equals(actualHash, expectedHash, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation document artifact '{path}' failed its identity or content proof.");
            }
        }

        static void ValidateOperationEntries(string root, string operationDirectory, MutationDocument document) {
            foreach (string entry in Directory.GetFileSystemEntries(operationDirectory, "*", SearchOption.TopDirectoryOnly)) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(entry, operationDirectory);
                string name = Path.GetFileName(entry);
                if (!string.Equals(name, "document.json", StringComparison.Ordinal) &&
                    !string.Equals(name, "document.next", StringComparison.Ordinal) &&
                    !string.Equals(name, "staged", StringComparison.Ordinal) &&
                    !string.Equals(name, "backups", StringComparison.Ordinal) &&
                    !string.Equals(name, "deleting", StringComparison.Ordinal) &&
                    !string.Equals(name, "destination.old", StringComparison.Ordinal) &&
                    !string.Equals(name, "document.old", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation operation contains an unexpected artifact '{entry}'.");
                }
                if (name is "staged" or "backups" or "deleting") {
                    if (!Directory.Exists(entry)) {
                        throw new InvalidDataException($"The authoring mutation artifact '{entry}' must be a directory.");
                    }
                    EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(entry, operationDirectory);
                }
                if (name == "destination.old") {
                    if (string.IsNullOrWhiteSpace(document.DestinationOldRelativePath) ||
                        !string.Equals(document.DestinationOldRelativePath, "destination.old", StringComparison.Ordinal)) {
                        throw new InvalidDataException($"The authoring mutation operation contains an unrecorded former destination.");
                    }
                }
                if (name == "document.old") {
                    if (!string.Equals(document.DocumentOldRelativePath, "document.old", StringComparison.Ordinal)) {
                        throw new InvalidDataException($"The authoring mutation operation contains an unrecorded previous document.");
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(document.StagedRelativePath)) {
                string stagedPath = Path.GetFullPath(Path.Combine(operationDirectory, document.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                string stagedPrefix = Path.Combine(operationDirectory, "staged") + Path.DirectorySeparatorChar;
                StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!stagedPath.StartsWith(stagedPrefix, comparison)) {
                    throw new InvalidDataException("The staged authoring payload escaped its operation directory.");
                }
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(stagedPath, operationDirectory);
                string stagedDirectory = Path.Combine(operationDirectory, "staged");
                if (Directory.Exists(stagedDirectory)) {
                    foreach (string stagedEntry in Directory.GetFileSystemEntries(stagedDirectory, "*", SearchOption.TopDirectoryOnly)) {
                        bool expectedFinal = string.Equals(Path.GetFullPath(stagedEntry), stagedPath, comparison);
                        bool expectedPublishing = !string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath) &&
                            string.Equals(Path.GetFullPath(stagedEntry), Path.Combine(operationDirectory, document.PublishingPayloadRelativePath.Replace('/', Path.DirectorySeparatorChar)), comparison);
                        bool expectedWrite = string.Equals(document.Phase, "StagingAllocated", StringComparison.Ordinal) &&
                            string.Equals(Path.GetFullPath(stagedEntry), stagedPath + ".next", comparison);
                        if (!expectedFinal && !expectedPublishing && !expectedWrite) {
                            throw new InvalidDataException($"The staged authoring operation contains an unexpected payload '{stagedEntry}'.");
                        }
                    }
                }
            } else {
                string stagedDirectory = Path.Combine(operationDirectory, "staged");
                bool hasWriteInProgress = string.Equals(document.Phase, "StagingAllocated", StringComparison.Ordinal) &&
                    Directory.Exists(stagedDirectory) &&
                    Directory.GetFileSystemEntries(stagedDirectory).All(entry =>
                        string.Equals(Path.GetFileName(entry), "payload.next", StringComparison.Ordinal));
                if (Directory.Exists(stagedDirectory) && Directory.GetFileSystemEntries(stagedDirectory).Length != 0 && !hasWriteInProgress) {
                    throw new InvalidDataException("The staged authoring operation contains an unrecorded payload.");
                }
            }
        }

        static bool TryRecoverDeleteBeforePublished(string root, string journalPath, MutationDocument document) {
            string sourcePath = Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string deletingPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, sourcePath);
            string deletingIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, deletingPath);
            if (sourceIdentity == document.ExpectedSourceIdentity && deletingIdentity == "missing") {
                RetireDocument(root, journalPath);
                return true;
            }
            if (sourceIdentity == "missing" && deletingIdentity == document.ExpectedSourceIdentity) {
                if (!document.Kind.Equals("delete-directory", StringComparison.Ordinal)) {
                    string deletingHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, deletingPath);
                    if (!string.Equals(deletingHash, document.ExpectedSourceHash, StringComparison.Ordinal)) {
                        throw new InvalidOperationException($"The authoring deletion '{journalPath}' found changed deleting content.");
                    }
                }
                document.Phase = "Published";
                DurableCurrent?.Persist();
                RecoverPublishedDocument(root, journalPath, document);
                return true;
            }
            if (sourceIdentity == "missing" && deletingIdentity == "missing") {
                throw new InvalidOperationException($"The authoring deletion '{journalPath}' lost its source and deleting entry.");
            }
            throw new InvalidOperationException($"The authoring deletion '{journalPath}' found conflicting source and deleting entries.");
        }

        static void TryRecoverTransientEntries(string root, string journalPath, MutationDocument document) {
            bool committed = string.Equals(document.Phase, "Published", StringComparison.Ordinal);
            // The list is a mutation graph ordered by reservation. Reverse
            // replay restores a replacement's source before its former
            // destination, while committed recovery retires each quarantine.
            foreach (TransientMutation transient in document.TransientEntries.AsEnumerable().Reverse().ToArray()) {
                string originalPath = Path.Combine(root, transient.OriginalRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string quarantinePath = Path.Combine(root, transient.QuarantineRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string intendedPath = string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath)
                    ? null
                    : Path.Combine(root, transient.IntendedDestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string originalIdentity = VerifyTransientLocation(root, originalPath, transient, "original");
                string quarantineIdentity = VerifyTransientLocation(root, quarantinePath, transient, "quarantine");
                string intendedIdentity = intendedPath == null
                    ? "missing"
                    : VerifyTransientLocation(root, intendedPath, transient, "intended destination");

                if (transient.Lifecycle == "Reserved") {
                    if (quarantineIdentity == "missing" && originalIdentity == transient.ExpectedIdentity) {
                        CompleteTransient(quarantinePath);
                        continue;
                    }
                    if (quarantineIdentity == transient.ExpectedIdentity && originalIdentity == "missing") {
                        transient.Lifecycle = "Occupied";
                        DurableCurrent?.Persist();
                        quarantineIdentity = transient.ExpectedIdentity;
                    } else {
                        throw new InvalidOperationException($"The authoring mutation '{journalPath}' has an unresolved reserved transient.");
                    }
                }

                if (committed) {
                    RecoverCommittedTransient(root, journalPath, document, transient, originalPath, quarantinePath, intendedPath, originalIdentity, quarantineIdentity, intendedIdentity);
                } else {
                    RecoverRollbackTransient(root, journalPath, transient, originalPath, quarantinePath, intendedPath, originalIdentity, quarantineIdentity, intendedIdentity);
                }
            }
            RetireDocument(root, journalPath);
        }

        static string VerifyTransientLocation(string root, string path, TransientMutation transient, string label) {
            string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
            if (identity == "missing") {
                return identity;
            }
            if (identity == "unavailable" || !string.Equals(identity, transient.ExpectedIdentity, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The authoring mutation found a changed {label} entry '{path}'.");
            }
            if (transient.EntryKind == "File") {
                string hash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, path);
                if (!string.Equals(hash, transient.ExpectedHash, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The authoring mutation found changed {label} content '{path}'.");
                }
            }
            return identity;
        }

        static void RecoverRollbackTransient(
            string root,
            string journalPath,
            TransientMutation transient,
            string originalPath,
            string quarantinePath,
            string intendedPath,
            string originalIdentity,
            string quarantineIdentity,
            string intendedIdentity) {
            if (transient.RecoveryIntent == "RollbackPublication") {
                if (intendedPath == null) {
                    throw new InvalidDataException($"The authoring mutation '{journalPath}' has no intended destination.");
                }
                if (quarantineIdentity == transient.ExpectedIdentity && originalIdentity == "missing") {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, quarantinePath, originalPath, transient.ExpectedIdentity, "missing", transient.ExpectedHash);
                    CompleteTransient(quarantinePath);
                    return;
                }
                if (quarantineIdentity == "missing" && originalIdentity == "missing" && intendedIdentity == transient.ExpectedIdentity) {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, intendedPath, originalPath, transient.ExpectedIdentity, "missing", transient.ExpectedHash);
                    CompleteTransient(quarantinePath);
                    return;
                }
                if (quarantineIdentity == "missing" && originalIdentity == transient.ExpectedIdentity && intendedIdentity == "missing") {
                    CompleteTransient(quarantinePath);
                    return;
                }
                throw new InvalidOperationException($"The authoring mutation '{journalPath}' has conflicting publication entries.");
            }

            if (transient.RecoveryIntent == "RestoreOriginal") {
                if (quarantineIdentity == transient.ExpectedIdentity && originalIdentity == "missing") {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, quarantinePath, originalPath, transient.ExpectedIdentity, "missing", transient.ExpectedHash);
                    CompleteTransient(quarantinePath);
                    return;
                }
                if (quarantineIdentity == "missing" && originalIdentity == transient.ExpectedIdentity) {
                    CompleteTransient(quarantinePath);
                    return;
                }
                throw new InvalidOperationException($"The authoring mutation '{journalPath}' has conflicting restoration entries.");
            }
            throw new InvalidDataException($"The authoring mutation '{journalPath}' contains an unsupported transient recovery intent.");
        }

        static void RecoverCommittedTransient(
            string root,
            string journalPath,
            MutationDocument document,
            TransientMutation transient,
            string originalPath,
            string quarantinePath,
            string intendedPath,
            string originalIdentity,
            string quarantineIdentity,
            string intendedIdentity) {
            if (transient.RecoveryIntent == "RollbackPublication") {
                if (intendedPath == null) {
                    throw new InvalidDataException($"The authoring mutation '{journalPath}' has no intended destination.");
                }
                if (quarantineIdentity == transient.ExpectedIdentity && intendedIdentity == "missing") {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, quarantinePath, intendedPath, transient.ExpectedIdentity, "missing", transient.ExpectedHash);
                    intendedIdentity = VerifyTransientLocation(root, intendedPath, transient, "published destination");
                }
                if (quarantineIdentity == "missing" && intendedIdentity == transient.ExpectedIdentity) {
                    CompleteTransient(quarantinePath);
                    return;
                }
                throw new InvalidOperationException($"The committed authoring mutation '{journalPath}' lost a publication transient.");
            }

            if (!string.Equals(transient.RecoveryIntent, "RestoreOriginal", StringComparison.Ordinal)) {
                throw new InvalidDataException($"The committed authoring mutation '{journalPath}' contains an unsupported cleanup intent.");
            }
            VerifyCurrentPublicationDestination(root, document);
            if (quarantineIdentity != "missing" && originalIdentity != "missing") {
                throw new InvalidOperationException($"The committed authoring mutation '{journalPath}' contains both restoration entries.");
            }
            if (!string.Equals(transient.Lifecycle, "CleanupPending", StringComparison.Ordinal)) {
                transient.Lifecycle = "CleanupPending";
                DurableCurrent?.Persist();
            }
            if (quarantineIdentity != "missing") {
                VerifyTransientLocation(root, quarantinePath, transient, "cleanup quarantine");
                if (transient.EntryKind == "Directory") {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, quarantinePath, Path.GetDirectoryName(quarantinePath), transient.ExpectedIdentity);
                } else {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, quarantinePath, transient.ExpectedIdentity, transient.ExpectedHash);
                }
            }
            EditorAuthoringMutationScope.FlushContainingDirectoryForRecovery(root, quarantinePath, "TransientCleanup.FlushParent");
            CompleteTransient(quarantinePath);
        }

        void Persist() {
            EnsureMutationCallbackAllowed();
            WriteDocument(JournalPath, Document, ProjectRootPath, createNew: false);
        }

        public void Dispose() {
            if (ReferenceEquals(Current.Value, this)) {
                Current.Value = PreviousCurrent;
            }
            // Complete normally releases this handle in its finally block;
            // retry it here as well when release itself reported a failure so
            // a using-boundary can make disposal retryable.
            ProjectWriteLock?.Dispose();
        }

        static void WriteDocument(string path, MutationDocument document, string root, bool createNew) {
            if (IsWritingDocument) {
                throw new InvalidOperationException("Journal document persistence cannot be entered recursively.");
            }
            using IDisposable documentWriteScope = EnterDocumentWriteScope();
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, Path.GetDirectoryName(path));
            document.Sequence++;
            string operationDirectory = Path.GetDirectoryName(path);
            string nextPath = Path.Combine(operationDirectory, "document.next");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(nextPath, operationDirectory);
            // Keep the operation namespace limited to its fixed artifacts. The
            // next document is written through its verified handle and then
            // atomically promoted; an incomplete next document is discarded
            // only after the current document has been proved valid by Recover.
            if (createNew) {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = false });
                EditorAuthoringMutationScope.FixedCreateExclusive(root, nextPath, bytes);
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, nextPath, path, null, "missing");
            } else {
                string currentIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
                string currentHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, path);
                if (currentIdentity is "missing" or "unavailable" || currentHash is "missing" or "unavailable") {
                    throw new InvalidDataException($"The authoring mutation document '{path}' could not be proved before replacement.");
                }
                document.DocumentOldRelativePath = "document.old";
                document.DocumentOldIdentity = currentIdentity;
                document.DocumentOldHash = currentHash;
                string oldPath = Path.Combine(operationDirectory, "document.old");
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(oldPath, operationDirectory);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = false });
                EditorAuthoringMutationScope.FixedWrite(root, nextPath, bytes);
                EditorAuthoringMutationScope.FixedRenameNoReplace(
                    root,
                    path,
                    oldPath,
                    currentIdentity,
                    "missing",
                    currentHash);
                string nextIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, nextPath);
                string nextHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, nextPath);
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, nextPath, path, nextIdentity, "missing", nextHash);
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, oldPath, currentIdentity, currentHash);
            }
        }

        static string NormalizeRelativePath(string root, string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new InvalidDataException("Authoring mutation journal paths must be provided.");
            }
            string full = Path.GetFullPath(path);
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(prefix, comparison)) {
                throw new InvalidDataException("Authoring mutation journal paths must remain beneath the project root.");
            }
            return Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
        }

        static string CaptureIdentity(string root, string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return "missing";
            }
            try {
                return EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, path);
            } catch {
                return "unavailable";
            }
        }

        static string CaptureHash(string root, string path) {
            string identity = CaptureIdentity(root, path);
            if (identity == "missing" || identity == "unavailable") {
                return identity;
            }
            if (identity.EndsWith(":directory", StringComparison.Ordinal)) {
                return "directory";
            }
            return EditorAuthoringMutationScope.TryGetVerifiedSha256(root, path);
        }

        static void ValidateDocument(MutationDocument document, string path, string root, bool allowNextDocument = false) {
            if (document == null || document.Version != CurrentVersion || document.Sequence <= 0 || string.IsNullOrWhiteSpace(document.OperationId) ||
                !Guid.TryParseExact(document.OperationId, "N", out _) || string.IsNullOrWhiteSpace(document.Kind) ||
                !SupportedKinds.Contains(document.Kind) ||
                string.IsNullOrWhiteSpace(document.Phase) ||
                document.TransientEntries == null) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is invalid.");
            }
            ValidateDocumentRelativePath(document.SourceRelativePath, root, "source");
            ValidateDocumentRelativePath(document.DestinationRelativePath, root, "destination");
            if (string.IsNullOrWhiteSpace(document.ExpectedSourceIdentity) ||
                string.IsNullOrWhiteSpace(document.ExpectedDestinationIdentity)) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing entry identity proofs.");
            }
            if (!string.Equals(document.Phase, "Prepared", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "StagingAllocated", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Staged", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "PayloadPublishing", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "DestinationQuarantined", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Publishing", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Published", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Completed", StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' contains an unsupported phase '{document.Phase}'.");
            }
            if (!string.Equals(document.Phase, "Completed", StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(document.ExpectedDestinationHash)) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing destination content proof.");
            }
            bool sourceIsDirectory = document.ExpectedSourceIdentity?.EndsWith(":directory", StringComparison.Ordinal) == true ||
                document.ExpectedSourceIdentity?.EndsWith(";directory", StringComparison.Ordinal) == true ||
                document.ExpectedSourceIdentity?.EndsWith("type:4000", StringComparison.OrdinalIgnoreCase) == true;
            bool sourceIsMissing = string.Equals(document.ExpectedSourceIdentity, "missing", StringComparison.Ordinal);
            if (!string.Equals(document.Phase, "Completed", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(document.ExpectedSourceHash) ||
                 (!sourceIsMissing && !sourceIsDirectory && !IsSha256Hash(document.ExpectedSourceHash)) ||
                 (sourceIsMissing && !string.Equals(document.ExpectedSourceHash, "missing", StringComparison.Ordinal)) ||
                 (sourceIsDirectory && !string.Equals(document.ExpectedSourceHash, "directory", StringComparison.Ordinal)))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing source content proof.");
            }
            if (document.TransientEntries.Count > 0) {
                if (!string.Equals(document.Kind, "move", StringComparison.Ordinal) &&
                    !string.Equals(document.Kind, "replace", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains transients for a non-publication operation.");
                }
                if (!string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal) &&
                    !string.Equals(document.Phase, "Published", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains transients in phase '{document.Phase}'.");
                }
                if (string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal) &&
                    !SupportedTransientResumePhases.Contains(document.ResumePhase ?? string.Empty)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' is missing a valid resume phase.");
                }
                if (string.Equals(document.Phase, "Published", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(document.ResumePhase)) {
                    throw new InvalidDataException($"The published authoring mutation '{path}' retains a resume phase.");
                }
            } else if (string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' contains an empty quarantining graph.");
            }
            if (!string.IsNullOrWhiteSpace(document.StagedRelativePath) &&
                (string.IsNullOrWhiteSpace(document.StagedExactHash) || string.IsNullOrWhiteSpace(document.StagedIdentity))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing staged payload identity proof.");
            }
            ValidateFixedArtifactPath(document.PublishingPayloadRelativePath, root, path, "staged/payload.publishing");
            ValidateFixedArtifactPath(document.DestinationOldRelativePath, root, path, "destination.old");
            if (!string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath) &&
                (string.IsNullOrWhiteSpace(document.PublishingPayloadExactHash) || string.IsNullOrWhiteSpace(document.PublishingPayloadIdentity))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing publishing payload identity proof.");
            }
            if (!string.IsNullOrWhiteSpace(document.DestinationOldRelativePath) &&
                (string.IsNullOrWhiteSpace(document.DestinationOldHash) || string.IsNullOrWhiteSpace(document.DestinationOldIdentity))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing former destination identity proof.");
            }
            ValidateFixedArtifactPath(document.DocumentOldRelativePath, root, path, "document.old");
            if (!string.IsNullOrWhiteSpace(document.DocumentOldRelativePath) &&
                (string.IsNullOrWhiteSpace(document.DocumentOldHash) || string.IsNullOrWhiteSpace(document.DocumentOldIdentity))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing previous document identity proof.");
            }
            string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
            string expectedPrefix = Path.GetFullPath(journalDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string operationDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!operationDirectory.StartsWith(expectedPrefix, pathComparison) ||
                !Guid.TryParseExact(Path.GetFileName(operationDirectory), "N", out _) ||
                !string.Equals(Path.GetFileName(operationDirectory), document.OperationId, StringComparison.OrdinalIgnoreCase) ||
                !(string.Equals(Path.GetFileName(path), "document.json", StringComparison.Ordinal) ||
                  (allowNextDocument && string.Equals(Path.GetFileName(path), "document.next", StringComparison.Ordinal)))) {
                throw new InvalidDataException("The authoring mutation journal escaped its project journal directory.");
            }
            HashSet<string> transientQuarantinePaths = new HashSet<string>(pathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            HashSet<string> transientOriginalPaths = new HashSet<string>(pathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            HashSet<string> transientIntendedPaths = new HashSet<string>(pathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            foreach (TransientMutation transient in document.TransientEntries) {
                if (transient == null || string.IsNullOrWhiteSpace(transient.OriginalRelativePath) ||
                    string.IsNullOrWhiteSpace(transient.QuarantineRelativePath) ||
                    string.IsNullOrWhiteSpace(transient.EntryKind) ||
                    string.IsNullOrWhiteSpace(transient.ExpectedIdentity) ||
                    string.IsNullOrWhiteSpace(transient.RecoveryIntent) ||
                    string.IsNullOrWhiteSpace(transient.Lifecycle) ||
                    !SupportedTransientEntryKinds.Contains(transient.EntryKind) ||
                    !SupportedTransientRecoveryIntents.Contains(transient.RecoveryIntent) ||
                    !SupportedTransientLifecycles.Contains(transient.Lifecycle)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains an invalid transient entry.");
                }
                if (string.Equals(transient.RecoveryIntent, "RestoreOriginal", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' gives a restoration entry an intended destination.");
                }
                if (string.Equals(transient.RecoveryIntent, "RollbackPublication", StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' omits a publication destination.");
                }
                if (string.Equals(transient.Lifecycle, "CleanupPending", StringComparison.Ordinal) &&
                    !string.Equals(transient.RecoveryIntent, "RestoreOriginal", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' has an invalid cleanup lifecycle.");
                }
                bool isDirectory = string.Equals(transient.EntryKind, "Directory", StringComparison.Ordinal);
                if ((isDirectory && !string.IsNullOrWhiteSpace(transient.ExpectedHash)) ||
                    (!isDirectory && string.IsNullOrWhiteSpace(transient.ExpectedHash))) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains an invalid transient content proof.");
                }
                if (!IsSupportedTransientIdentity(transient.ExpectedIdentity, isDirectory) ||
                    (!isDirectory && !IsSha256Hash(transient.ExpectedHash))) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains an invalid transient identity or hash proof.");
                }
                ValidateDocumentRelativePath(transient.OriginalRelativePath, root, "transient original");
                ValidateDocumentRelativePath(transient.QuarantineRelativePath, root, "transient quarantine");
                if (!string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath)) {
                    ValidateDocumentRelativePath(transient.IntendedDestinationRelativePath, root, "transient destination");
                }
                if (string.Equals(transient.OriginalRelativePath, transient.QuarantineRelativePath, pathComparison) ||
                    (!string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath) &&
                     (string.Equals(transient.OriginalRelativePath, transient.IntendedDestinationRelativePath, pathComparison) ||
                      string.Equals(transient.QuarantineRelativePath, transient.IntendedDestinationRelativePath, pathComparison)))) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains colliding transient paths.");
                }
                if (transientIntendedPaths.Contains(transient.QuarantineRelativePath) ||
                    !transientQuarantinePaths.Add(transient.QuarantineRelativePath) ||
                    !transientOriginalPaths.Add(transient.OriginalRelativePath)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains duplicate transient paths.");
                }
                if (!string.IsNullOrWhiteSpace(transient.IntendedDestinationRelativePath) &&
                    (!transientIntendedPaths.Add(transient.IntendedDestinationRelativePath) ||
                     transientQuarantinePaths.Contains(transient.IntendedDestinationRelativePath))) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains colliding transient paths.");
                }
                string transientName = Path.GetFileName(transient.QuarantineRelativePath);
                string quarantineParent = Path.GetDirectoryName(transient.QuarantineRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string originalParent = Path.GetDirectoryName(transient.OriginalRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!transientName.StartsWith(".authoring-mutation-" + document.OperationId + "-", StringComparison.Ordinal) ||
                    !string.Equals(quarantineParent, originalParent, pathComparison)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains an unowned transient entry.");
                }
            }

            // An intended destination may depend on another record's original
            // path (the former destination in a replacement graph), but the
            // dependency graph must remain acyclic. A cycle would leave two
            // entries waiting for one another and would make recovery choose
            // a name rather than a recorded mutation order.
            Dictionary<string, int> originalOwners = new Dictionary<string, int>(
                pathComparison == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal);
            for (int index = 0; index < document.TransientEntries.Count; index++) {
                originalOwners.Add(document.TransientEntries[index].OriginalRelativePath, index);
            }
            int[] visitState = new int[document.TransientEntries.Count];
            for (int index = 0; index < document.TransientEntries.Count; index++) {
                ValidateTransientDependency(index);
            }

            void ValidateTransientDependency(int index) {
                if (visitState[index] == 2) {
                    return;
                }
                if (visitState[index] == 1) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains a cyclic transient graph.");
                }
                visitState[index] = 1;
                string intended = document.TransientEntries[index].IntendedDestinationRelativePath;
                if (!string.IsNullOrWhiteSpace(intended) &&
                    originalOwners.TryGetValue(intended, out int dependency) &&
                    dependency != index) {
                    ValidateTransientDependency(dependency);
                }
                visitState[index] = 2;
            }
        }

        static bool IsSupportedTransientIdentity(string identity, bool directory) {
            if (string.IsNullOrWhiteSpace(identity) ||
                identity is "missing" or "unavailable") {
                return false;
            }
            if (identity.StartsWith("dev:", StringComparison.Ordinal)) {
                return identity.Contains(";inode:", StringComparison.Ordinal) &&
                    identity.Contains(";type:", StringComparison.Ordinal) &&
                    identity.EndsWith($"type:{(directory ? "4000" : "8000")}", StringComparison.OrdinalIgnoreCase);
            }
            if (identity.StartsWith("windows:", StringComparison.Ordinal)) {
                return identity.EndsWith(directory ? ":directory" : ":file", StringComparison.Ordinal);
            }
            return false;
        }

        static bool IsSha256Hash(string hash) {
            if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64) {
                return false;
            }
            foreach (char character in hash) {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f') ||
                      (character >= 'A' && character <= 'F'))) {
                    return false;
                }
            }
            return true;
        }

        static void ValidateDocumentRelativePath(string relativePath, string root, string label) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                throw new InvalidDataException($"The authoring mutation journal contains an invalid {label} path.");
            }
            string[] segments = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.None);
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")) {
                throw new InvalidDataException($"The authoring mutation journal contains an invalid {label} path.");
            }
            string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison)) {
                throw new InvalidDataException($"The authoring mutation journal {label} path escaped the project root.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, root);
        }

        static void ValidateFixedArtifactPath(string relativePath, string root, string journalPath, string expectedRelativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return;
            }
            if (!string.Equals(relativePath.Replace('\\', '/'), expectedRelativePath, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation journal '{journalPath}' contains an invalid fixed artifact path.");
            }
            string fullPath = Path.Combine(root, "cache", "editor", JournalDirectoryName, Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(journalPath))), relativePath.Replace('/', Path.DirectorySeparatorChar));
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, Path.GetDirectoryName(Path.GetFullPath(journalPath)));
        }

        static void RecoverPublishedDocument(string root, string journalPath, MutationDocument document) {
            string sourcePath = Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
            if (document.Kind.StartsWith("delete", StringComparison.Ordinal)) {
                if (destinationIdentity == "missing") {
                    if (EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, sourcePath) != "missing") {
                        throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found the original entry alongside its deleting entry.");
                    }
                    EditorAuthoringMutationScope.FlushContainingDirectoryForRecovery(root, destinationPath, "Recovery.BeforeDeleteRetire");
                    RetireDocument(root, journalPath);
                    return;
                }
                if (!string.Equals(destinationIdentity, document.ExpectedSourceIdentity, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found a changed deleting entry.");
                }
                if (!document.Kind.Equals("delete-directory", StringComparison.Ordinal)) {
                    string deletingHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                    if (!string.Equals(deletingHash, document.ExpectedSourceHash, StringComparison.Ordinal)) {
                        throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found changed deleting content.");
                    }
                }
                if (document.Kind.Equals("delete-directory", StringComparison.Ordinal)) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, destinationPath, Path.GetDirectoryName(destinationPath), document.ExpectedSourceIdentity);
                } else {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, destinationPath, document.ExpectedSourceIdentity);
                }
                RetireDocument(root, journalPath);
                return;
            }

            // A staged payload publication has a different identity from the
            // pre-existing source. Its content hash is authoritative after
            // the fixed publish syscall, including when the process stopped
            // before the phase document advanced.
            if (!string.IsNullOrWhiteSpace(document.StagedExactHash)) {
                string destinationHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                if (string.Equals(destinationHash, document.StagedExactHash, StringComparison.Ordinal)) {
                    RetireDocument(root, journalPath);
                    return;
                }
            }

            if (string.Equals(document.ExpectedSourceIdentity, "missing", StringComparison.Ordinal) ||
                string.Equals(document.ExpectedSourceIdentity, "unavailable", StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The published authoring mutation '{journalPath}' has no verifiable source identity.");
            }
            if (!string.Equals(destinationIdentity, document.ExpectedSourceIdentity, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The published authoring mutation '{journalPath}' found a changed destination entry.");
            }
            RetireDocument(root, journalPath);
        }

        static void RecoverPayloadPublication(string root, string journalPath, MutationDocument document) {
            string operationDirectory = Path.GetDirectoryName(journalPath);
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string stagedPath = string.IsNullOrWhiteSpace(document.StagedRelativePath)
                ? null
                : Path.Combine(operationDirectory, document.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string publishingPath = string.IsNullOrWhiteSpace(document.PublishingPayloadRelativePath)
                ? null
                : Path.Combine(operationDirectory, document.PublishingPayloadRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationOldPath = string.IsNullOrWhiteSpace(document.DestinationOldRelativePath)
                ? null
                : Path.Combine(operationDirectory, document.DestinationOldRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (document.Kind == "copy" && destinationOldPath != null) {
                throw new InvalidDataException($"The copy mutation '{journalPath}' cannot own a former destination.");
            }
            if (stagedPath != null) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(stagedPath, operationDirectory);
            }
            if (publishingPath != null) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(publishingPath, operationDirectory);
            }
            if (destinationOldPath != null) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destinationOldPath, operationDirectory);
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destinationPath, root);

            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
            string destinationHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
            string stagedIdentity = stagedPath == null ? "missing" : EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, stagedPath);
            string stagedHash = stagedPath == null ? "missing" : EditorAuthoringMutationScope.TryGetVerifiedSha256(root, stagedPath);
            string publishingIdentity = publishingPath == null ? "missing" : EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, publishingPath);
            string publishingHash = publishingPath == null ? "missing" : EditorAuthoringMutationScope.TryGetVerifiedSha256(root, publishingPath);
            string oldIdentity = destinationOldPath == null ? "missing" : EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationOldPath);
            string oldHash = destinationOldPath == null ? "missing" : EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationOldPath);

            bool stagedValid = stagedIdentity == document.StagedIdentity && stagedHash == document.StagedExactHash;
            bool publishingValid = publishingIdentity == document.PublishingPayloadIdentity && publishingHash == document.PublishingPayloadExactHash;
            bool destinationIsPublished = destinationIdentity == document.StagedIdentity && destinationHash == document.StagedExactHash;
            bool destinationIsOriginal = destinationIdentity == document.ExpectedDestinationIdentity && destinationHash == document.ExpectedDestinationHash;
            bool oldValid = destinationOldPath != null && oldIdentity == document.DestinationOldIdentity && oldHash == document.DestinationOldHash;

            // A completed no-replace publish is recognizable by the exact
            // staged inode/hash at the destination. Retire only after any
            // former destination is independently proven and removed.
            if (destinationIsPublished) {
                EditorAuthoringMutationScope.FlushContainingDirectoryForRecovery(
                    root,
                    destinationPath,
                    "Recovery.BeforePublishedDestinationFlush");
                EditorAuthoringMutationScope.FlushContainingDirectoryForRecovery(
                    root,
                    string.IsNullOrWhiteSpace(publishingPath) && string.IsNullOrWhiteSpace(stagedPath)
                        ? Path.Combine(operationDirectory, "staged")
                        : Path.GetDirectoryName(publishingPath ?? stagedPath),
                    "Recovery.BeforePublishedPayloadFlush");
                if (oldValid) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, destinationOldPath, oldIdentity, oldHash);
                } else if (destinationOldPath != null && oldIdentity != "missing") {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' found an unexpected former destination.");
                }
                if (publishingValid) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, publishingPath, publishingIdentity, publishingHash);
                }
                if (stagedValid) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, stagedPath, stagedIdentity, stagedHash);
                }
                RetireDocument(root, journalPath);
                return;
            }

            // A strict copy can only publish into an absent destination. An
            // unexpected destination is never overwritten during recovery.
            if (document.Kind == "copy") {
                if (destinationIdentity != "missing" && !destinationIsPublished) {
                    throw new InvalidOperationException($"The copy mutation '{journalPath}' found a destination collision.");
                }
                string payloadPath = publishingValid ? publishingPath : stagedValid ? stagedPath : null;
                string payloadIdentity = publishingValid ? publishingIdentity : stagedIdentity;
                string payloadHash = publishingValid ? publishingHash : stagedHash;
                if (destinationIdentity == "missing" && payloadPath != null) {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, payloadPath, destinationPath, payloadIdentity, "missing", payloadHash);
                    string publishedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                    string publishedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                    if (publishedIdentity != document.StagedIdentity || publishedHash != document.StagedExactHash) {
                        throw new InvalidOperationException($"The copy mutation '{journalPath}' published an unverifiable destination.");
                    }
                    RetireDocument(root, journalPath);
                    return;
                }
                throw new InvalidOperationException($"The copy mutation '{journalPath}' has no verifiable publication payload.");
            }

            // If the old destination has already been quarantined, publish the
            // exact payload while the journal owns the destination gap.
            if (oldValid && destinationIdentity == "missing") {
                string payloadPath = publishingValid ? publishingPath : stagedValid ? stagedPath : null;
                string payloadIdentity = publishingValid ? publishingIdentity : stagedIdentity;
                string payloadHash = publishingValid ? publishingHash : stagedHash;
                if (payloadPath == null) {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' lost its staged payload.");
                }
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, payloadPath, destinationPath, payloadIdentity, "missing", payloadHash);
                string publishedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                string publishedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                if (publishedIdentity != document.StagedIdentity || publishedHash != document.StagedExactHash) {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' published an unverifiable destination.");
                }
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, destinationOldPath, oldIdentity, oldHash);
                RetireDocument(root, journalPath);
                return;
            }

            // New destinations have no former inode. A crash between the
            // durable publishing intent and the final no-replace rename is
            // completed from whichever fixed payload name still proves the
            // staged identity.
            if (destinationIdentity == "missing" && oldIdentity == "missing" && destinationOldPath == null) {
                string payloadPath = publishingValid ? publishingPath : stagedValid ? stagedPath : null;
                string payloadIdentity = publishingValid ? publishingIdentity : stagedIdentity;
                string payloadHash = publishingValid ? publishingHash : stagedHash;
                if (payloadPath != null) {
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, payloadPath, destinationPath, payloadIdentity, "missing", payloadHash);
                    string publishedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                    string publishedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                    if (publishedIdentity != document.StagedIdentity || publishedHash != document.StagedExactHash) {
                        throw new InvalidOperationException($"The authoring mutation '{journalPath}' published an unverifiable destination.");
                    }
                    RetireDocument(root, journalPath);
                    return;
                }
            }

            // No destination gap means the operation has not quarantined the
            // old inode yet. If the process stopped before reserving the
            // fixed former-destination artifact, reserve and persist it now
            // before moving any namespace entry. The next recovery observes
            // the same exact proof even if the move itself is interrupted.
            if (destinationIsOriginal && document.Kind != "copy" && oldIdentity == "missing") {
                string payloadPath = publishingValid ? publishingPath : stagedValid ? stagedPath : null;
                string payloadIdentity = publishingValid ? publishingIdentity : stagedIdentity;
                string payloadHash = publishingValid ? publishingHash : stagedHash;
                if (payloadPath == null) {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' has no verifiable staged payload.");
                }
                if (destinationOldPath == null) {
                    document.DestinationOldRelativePath = "destination.old";
                    document.DestinationOldIdentity = document.ExpectedDestinationIdentity;
                    document.DestinationOldHash = document.ExpectedDestinationHash;
                    WriteDocument(journalPath, document, root, createNew: false);
                    destinationOldPath = Path.Combine(operationDirectory, "destination.old");
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(destinationOldPath, operationDirectory);
                }
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, destinationPath, destinationOldPath, document.ExpectedDestinationIdentity, "missing", document.ExpectedDestinationHash);
                string movedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationOldPath);
                string movedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationOldPath);
                if (movedIdentity != document.ExpectedDestinationIdentity || movedHash != document.ExpectedDestinationHash) {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' moved an unverifiable former destination.");
                }
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, payloadPath, destinationPath, payloadIdentity, "missing", payloadHash);
                string publishedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                string publishedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
                if (publishedIdentity != document.StagedIdentity || publishedHash != document.StagedExactHash) {
                    throw new InvalidOperationException($"The authoring mutation '{journalPath}' published an unverifiable destination.");
                }
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, destinationOldPath, movedIdentity, movedHash);
                RetireDocument(root, journalPath);
                return;
            }

            // A replacement may have stopped after the old inode was moved
            // but before a payload was made durable. Restore it exactly.
            if (destinationIsMissing(destinationIdentity) && oldValid && !stagedValid && !publishingValid) {
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, destinationOldPath, destinationPath, oldIdentity, "missing", oldHash);
                RetireDocument(root, journalPath);
                return;
            }
            throw new InvalidOperationException($"The authoring mutation '{journalPath}' has an ambiguous inode publication state.");
        }

        static bool destinationIsMissing(string identity) => identity == "missing";

        static void RecoverBareStagedPayload(string root, string journalPath, MutationDocument document) {
            if (!string.Equals(document.Kind, "copy", StringComparison.Ordinal) &&
                !string.Equals(document.Kind, "replace", StringComparison.Ordinal)) {
                throw new InvalidDataException($"The staged authoring mutation '{journalPath}' has no supported payload recovery kind.");
            }
            if (string.IsNullOrWhiteSpace(document.StagedRelativePath) ||
                string.IsNullOrWhiteSpace(document.StagedIdentity) ||
                string.IsNullOrWhiteSpace(document.StagedExactHash)) {
                throw new InvalidDataException($"The staged authoring mutation '{journalPath}' has no complete payload proof.");
            }

            string operationDirectory = Path.GetDirectoryName(journalPath);
            string stagedPath = Path.Combine(operationDirectory, document.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(stagedPath, operationDirectory);
            string stagedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, stagedPath);
            string stagedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, stagedPath);

            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
            string destinationHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
            if (!string.Equals(destinationIdentity, document.ExpectedDestinationIdentity, StringComparison.Ordinal) ||
                !string.Equals(destinationHash, document.ExpectedDestinationHash, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' found a changed destination.");
            }

            // A process may have stopped after recording the staged proof and
            // after the staged inode was removed, but before publication. If
            // the destination still has its exact pre-publication proof, the
            // operation is already safely aborted: retire it without trying
            // to recreate a payload that no longer exists.
            bool stagedMissing = stagedIdentity == "missing" && stagedHash == "missing";
            if (stagedMissing) {
                RetireDocument(root, journalPath);
                return;
            }
            if (!string.Equals(stagedIdentity, document.StagedIdentity, StringComparison.Ordinal) ||
                !string.Equals(stagedHash, document.StagedExactHash, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' found a changed payload.");
            }

            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, stagedPath, stagedIdentity, stagedHash);
            RetireDocument(root, journalPath);
        }

        static void RetireDocument(string root, string journalPath) {
            string operationDirectory = Path.GetDirectoryName(journalPath);
            string journalDirectory = Path.GetDirectoryName(operationDirectory);
            string operationId = Path.GetFileName(operationDirectory);
            if (!Guid.TryParseExact(operationId, "N", out _)) {
                throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' has no valid operation id.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(operationDirectory, journalDirectory);
            string deletingDirectory = Path.Combine(journalDirectory, ".deleting-" + operationId);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(deletingDirectory, journalDirectory);
            // Retiring a journal is part of its fixed document lifecycle. Keep
            // that lifecycle context active so its directory cleanup cannot
            // allocate another transient operation and persist recursively.
            using IDisposable documentWriteScope = EnterDocumentWriteScope();
            EditorAuthoringMutationScope.FixedRenameNoReplace(root, operationDirectory, deletingDirectory, null, "missing");
            EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, deletingDirectory, journalDirectory);
        }

        static MutationDocument ReadDocument(string path, string root) {
            return JsonSerializer.Deserialize<MutationDocument>(
                Encoding.UTF8.GetString(EditorAuthoringMutationScope.ReadAllBytes(root, path)));
        }

        static void ValidateTransitionDirectoryName(string name, string prefix) {
            string operationId = name.Substring(prefix.Length);
            if (!Guid.TryParseExact(operationId, "N", out _)) {
                throw new InvalidDataException($"The authoring mutation transition directory '{name}' has no valid operation id.");
            }
        }

        sealed class MutationDocument {
            public int Version { get; set; }
            public long Sequence { get; set; }
            public string OperationId { get; set; }
            public string Kind { get; set; }
            public string SourceRelativePath { get; set; }
            public string DestinationRelativePath { get; set; }
            public string ExpectedSourceIdentity { get; set; }
            public string ExpectedSourceHash { get; set; }
            public string ExpectedDestinationIdentity { get; set; }
            public string ExpectedDestinationHash { get; set; }
            public string StagedRelativePath { get; set; }
            public string StagedExactHash { get; set; }

            public string StagedIdentity { get; set; }
            public string PublishingPayloadRelativePath { get; set; }
            public string PublishingPayloadExactHash { get; set; }
            public string PublishingPayloadIdentity { get; set; }
            public string DestinationOldRelativePath { get; set; }
            public string DestinationOldHash { get; set; }
            public string DestinationOldIdentity { get; set; }
            public string DocumentOldRelativePath { get; set; }
            public string DocumentOldHash { get; set; }
            public string DocumentOldIdentity { get; set; }
            public string Phase { get; set; }
            public string ResumePhase { get; set; }
            public List<TransientMutation> TransientEntries { get; set; }
        }

        sealed class TransientMutation {
            public string OriginalRelativePath { get; set; }
            public string QuarantineRelativePath { get; set; }
            public string IntendedDestinationRelativePath { get; set; }
            public string EntryKind { get; set; }
            public string ExpectedIdentity { get; set; }
            public string ExpectedHash { get; set; }
            public string RecoveryIntent { get; set; }
            public string Lifecycle { get; set; }
        }
    }
}
