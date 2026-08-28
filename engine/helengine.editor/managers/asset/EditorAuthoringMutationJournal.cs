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
        static readonly AsyncLocal<EditorAuthoringMutationJournal> Current = new AsyncLocal<EditorAuthoringMutationJournal>();
        readonly string ProjectRootPath;
        readonly string JournalPath;
        readonly MutationDocument Document;
        readonly EditorAuthoringMutationJournal PreviousCurrent;
        readonly EditorProjectWriteLock ProjectWriteLock;
        readonly bool Ephemeral;
        readonly bool SuppressOuterEvents;
        bool Completed;
        int TransientSequence;

        EditorAuthoringMutationJournal(string projectRootPath, string journalPath, MutationDocument document, EditorAuthoringMutationJournal previousCurrent, EditorProjectWriteLock projectWriteLock) {
            ProjectRootPath = projectRootPath;
            JournalPath = journalPath;
            Document = document;
            PreviousCurrent = previousCurrent;
            ProjectWriteLock = projectWriteLock;
        }

        EditorAuthoringMutationJournal(string projectRootPath, EditorAuthoringMutationJournal previousCurrent, bool suppressOuterEvents) {
            ProjectRootPath = projectRootPath;
            PreviousCurrent = previousCurrent;
            Ephemeral = true;
            SuppressOuterEvents = suppressOuterEvents;
            Document = new MutationDocument {
                OperationId = Guid.NewGuid().ToString("N"),
                Phase = "Prepared",
                TransientEntries = new List<string>()
            };
        }

        internal static IDisposable EnterEphemeral(string projectRootPath, bool suppressOuterEvents = true) {
            EditorAuthoringMutationJournal operation = new EditorAuthoringMutationJournal(projectRootPath, Current.Value, suppressOuterEvents);
            Current.Value = operation;
            return operation;
        }

        internal string OperationDirectoryPath => Path.GetDirectoryName(JournalPath);

        internal string ExpectedSourceIdentityValue => Document.ExpectedSourceIdentity;

        internal string ExpectedDestinationIdentityValue => Document.ExpectedDestinationIdentity;

        internal string ExpectedDestinationHashValue => Document.ExpectedDestinationHash;

        internal string StagedIdentityValue => Document.StagedIdentity;

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
                    ExpectedDestinationIdentity = CaptureIdentity(root, destinationPath),
                    ExpectedDestinationHash = CaptureHash(root, destinationPath),
                    Phase = "Prepared",
                    TransientEntries = new List<string>()
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
            EditorAuthoringMutationJournal current = Current.Value;
            if (current?.Ephemeral == true && current.SuppressOuterEvents) {
                string safeEphemeralName = Path.GetFileName(originalName);
                string ephemeralName = ".authoring-mutation-" + current.Document.OperationId + "-" + current.TransientSequence++.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + safeEphemeralName;
                current.Document.TransientEntries.Add(ephemeralName);
                return ephemeralName;
            }
            EditorAuthoringMutationJournal journal = DurableCurrent;
            if (journal == null) {
                throw new InvalidOperationException("An authoring mutation transient requires an active operation boundary.");
            }
            string safeName = Path.GetFileName(originalName);
            string transientName = ".authoring-mutation-" + journal.Document.OperationId + "-" + journal.TransientSequence++.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + safeName;
            journal.Document.TransientEntries.Add(transientName);
            journal.Document.Phase = "Quarantining";
            journal.Persist();
            return transientName;
        }

        internal void MarkPhase(string phase) {
            if (Completed) {
                return;
            }
            Document.Phase = phase ?? throw new ArgumentNullException(nameof(phase));
            Persist();
        }

        internal static void MarkCurrentPhase(string phase) {
            DurableCurrent?.MarkPhase(phase);
        }

        internal static void SetCurrentExpectedIdentities(string sourceIdentity, string destinationIdentity = null) {
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
            get {
                EditorAuthoringMutationJournal journal = Current.Value;
                if (journal?.Ephemeral == true && journal.SuppressOuterEvents) {
                    return null;
                }
                while (journal != null && journal.Ephemeral) {
                    journal = journal.PreviousCurrent;
                }
                return journal;
            }
        }

        internal void Complete() {
            if (Completed) {
                return;
            }
            try {
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
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, operationDirectory);
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(nextPath, operationDirectory);
                bool hasDocument = EntryExists(root, path);
                bool hasNextDocument = EntryExists(root, nextPath);
                if (!hasDocument && !hasNextDocument) {
                    throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' has no document.");
                }
                if (hasNextDocument) {
                    MutationDocument nextDocument = null;
                    Exception nextDocumentFailure = null;
                    try {
                        nextDocument = ReadDocument(nextPath, root);
                        ValidateDocument(nextDocument, nextPath, root, allowNextDocument: true);
                    } catch (Exception exception) {
                        nextDocumentFailure = exception;
                    }
                    if (nextDocumentFailure != null && !hasDocument) {
                        throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' has no valid document.", nextDocumentFailure);
                    }
                    MutationDocument currentDocument = null;
                    if (hasDocument) {
                        try {
                            currentDocument = ReadDocument(path, root);
                            ValidateDocument(currentDocument, path, root, allowNextDocument: false);
                        } catch (Exception exception) {
                            if (nextDocumentFailure != null) {
                                throw new InvalidDataException($"The authoring mutation operation '{operationDirectory}' contains no valid document.", exception);
                            }
                        }
                    }
                    if (nextDocumentFailure != null) {
                        // A torn next document is safe to discard only after
                        // the durable current document has been validated.
                        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
                    } else if (!hasDocument) {
                        using EditorAuthoringMutationScope operationScope = EditorAuthoringMutationScope.AcquireForMutation(root, operationDirectory);
                        EditorAuthoringMutationScope.FixedRenameNoReplace(root, nextPath, path, null, "missing");
                    } else {
                        if (currentDocument == null || nextDocument.Sequence > currentDocument.Sequence) {
                            using EditorAuthoringMutationScope operationScope = EditorAuthoringMutationScope.AcquireForMutation(root, operationDirectory);
                            EditorAuthoringMutationScope.FixedRenameExchange(root, nextPath, path);
                            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
                        } else {
                            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
                        }
                    }
                }
                MutationDocument document;
                try {
                    document = ReadDocument(path, root);
                } catch (Exception exception) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' is malformed.", exception);
                }
                ValidateDocument(document, path, root, allowNextDocument: false);
                ValidateOperationEntries(root, operationDirectory, document);
                if (string.Equals(document.Phase, "Prepared", StringComparison.Ordinal)) {
                    if (document.Kind.StartsWith("delete", StringComparison.Ordinal) &&
                        TryRecoverDeleteBeforePublished(root, path, document)) {
                        continue;
                    }
                    RetireDocument(root, path);
                    continue;
                }
                if (string.Equals(document.Phase, "Staged", StringComparison.Ordinal)) {
                    RecoverStagedDocument(root, path, document);
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
                if (string.Equals(document.Phase, "Publishing", StringComparison.Ordinal)) {
                    RecoverStagedDocument(root, path, document);
                    continue;
                }
                if (string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal)) {
                    if (document.Kind.StartsWith("delete", StringComparison.Ordinal) &&
                        TryRecoverDeleteBeforePublished(root, path, document)) {
                        continue;
                    }
                    if (TryRecoverQuarantiningDocument(root, path, document)) {
                        continue;
                    }
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

        static void ValidateOperationEntries(string root, string operationDirectory, MutationDocument document) {
            foreach (string entry in Directory.GetFileSystemEntries(operationDirectory, "*", SearchOption.TopDirectoryOnly)) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(entry, operationDirectory);
                string name = Path.GetFileName(entry);
                if (!string.Equals(name, "document.json", StringComparison.Ordinal) &&
                    !string.Equals(name, "document.next", StringComparison.Ordinal) &&
                    !string.Equals(name, "staged", StringComparison.Ordinal) &&
                    !string.Equals(name, "backups", StringComparison.Ordinal) &&
                    !string.Equals(name, "deleting", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation operation contains an unexpected artifact '{entry}'.");
                }
                if (name is "staged" or "backups" or "deleting") {
                    if (!Directory.Exists(entry)) {
                        throw new InvalidDataException($"The authoring mutation artifact '{entry}' must be a directory.");
                    }
                    EditorAuthoringTransactionRecoveryService.ValidateTreeHasNoReparsePoints(entry, operationDirectory);
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
                        bool expectedWrite = string.Equals(document.Phase, "StagingAllocated", StringComparison.Ordinal) &&
                            string.Equals(Path.GetFullPath(stagedEntry), stagedPath + ".next", comparison);
                        if (!expectedFinal && !expectedWrite) {
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
                if (document.Kind.Equals("delete-directory", StringComparison.Ordinal)) {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(root, deletingPath, Path.GetDirectoryName(deletingPath), document.ExpectedSourceIdentity);
                } else {
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, deletingPath, document.ExpectedSourceIdentity);
                }
                RetireDocument(root, journalPath);
                return true;
            }
            if (sourceIdentity == "missing" && deletingIdentity == "missing") {
                throw new InvalidOperationException($"The authoring deletion '{journalPath}' lost its source and deleting entry.");
            }
            throw new InvalidOperationException($"The authoring deletion '{journalPath}' found conflicting source and deleting entries.");
        }

        static bool TryRecoverQuarantiningDocument(string root, string journalPath, MutationDocument document) {
            string sourcePath = Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, sourcePath);
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);

            // A rename/exchange may have completed before the durable phase
            // update. Treat the destination as published only when its inode
            // is exactly the one proved before the operation; no name-only
            // recovery is allowed.
            if (sourceIdentity == "missing" && destinationIdentity == document.ExpectedSourceIdentity) {
                string sourceParent = Path.GetDirectoryName(sourcePath);
                for (int transientIndex = 0; transientIndex < document.TransientEntries.Count; transientIndex++) {
                    string transientPath = Path.Combine(sourceParent, document.TransientEntries[transientIndex]);
                    string transientIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, transientPath);
                    if (transientIdentity == "missing") {
                        continue;
                    }
                    if (transientIdentity != document.ExpectedDestinationIdentity) {
                        throw new InvalidOperationException($"The authoring mutation '{journalPath}' found a changed quarantine entry.");
                    }
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, transientPath, document.ExpectedDestinationIdentity);
                }
                RetireDocument(root, journalPath);
                return true;
            }

            // If the operation stopped after quarantine but before publication,
            // return the verified source inode to its original name.
            if (sourceIdentity == "missing") {
                string sourceParent = Path.GetDirectoryName(sourcePath);
                for (int transientIndex = 0; transientIndex < document.TransientEntries.Count; transientIndex++) {
                    string transientPath = Path.Combine(sourceParent, document.TransientEntries[transientIndex]);
                    string transientIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, transientPath);
                    if (transientIdentity == "missing") {
                        continue;
                    }
                    if (transientIdentity != document.ExpectedSourceIdentity) {
                        throw new InvalidOperationException($"The authoring mutation '{journalPath}' found a changed quarantine entry.");
                    }
                    EditorAuthoringMutationScope.FixedRenameNoReplace(root, transientPath, sourcePath, document.ExpectedSourceIdentity, "missing");
                }
                sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, sourcePath);
            }

            if (sourceIdentity == document.ExpectedSourceIdentity &&
                (destinationIdentity == document.ExpectedDestinationIdentity ||
                 string.Equals(document.SourceRelativePath, document.DestinationRelativePath, StringComparison.Ordinal))) {
                RetireDocument(root, journalPath);
                return true;
            }
            throw new InvalidOperationException($"The authoring mutation '{journalPath}' is unresolved; startup is blocked until it is repaired.");
        }

        void Persist() {
            if (Ephemeral) {
                return;
            }
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
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, Path.GetDirectoryName(path));
            document.Sequence++;
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = false });
            string operationDirectory = Path.GetDirectoryName(path);
            string nextPath = Path.Combine(operationDirectory, "document.next");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(nextPath, operationDirectory);
            // Keep the operation namespace limited to its fixed artifacts. The
            // next document is written through its verified handle and then
            // atomically promoted; an incomplete next document is discarded
            // only after the current document has been proved valid by Recover.
            if (createNew) {
                EditorAuthoringMutationScope.FixedCreateExclusive(root, nextPath, bytes);
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, nextPath, path, null, "missing");
            } else {
                EditorAuthoringMutationScope.FixedWrite(root, nextPath, bytes);
                EditorAuthoringMutationScope.FixedRenameExchange(root, nextPath, path);
                // On POSIX the exchanged old document remains at document.next;
                // on Windows the verified replace consumes it. Deleting an
                // already absent fixed leaf is intentionally idempotent.
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, nextPath);
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
            if (!string.IsNullOrWhiteSpace(document.StagedRelativePath) &&
                (string.IsNullOrWhiteSpace(document.StagedExactHash) || string.IsNullOrWhiteSpace(document.StagedIdentity))) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' is missing staged payload identity proof.");
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
            foreach (string transient in document.TransientEntries) {
                if (string.IsNullOrWhiteSpace(transient) || transient.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 || !transient.StartsWith(".authoring-mutation-", StringComparison.Ordinal)) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' contains an invalid transient entry.");
                }
            }
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

        static void RecoverPublishedDocument(string root, string journalPath, MutationDocument document) {
            string sourcePath = Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
            if (document.Kind.StartsWith("delete", StringComparison.Ordinal)) {
                if (destinationIdentity == "missing") {
                    if (EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, sourcePath) != "missing") {
                        throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found the original entry alongside its deleting entry.");
                    }
                    RetireDocument(root, journalPath);
                    return;
                }
                if (!string.Equals(destinationIdentity, document.ExpectedSourceIdentity, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found a changed deleting entry.");
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
            for (int index = 0; index < document.TransientEntries.Count; index++) {
                string transientPath = Path.Combine(Path.GetDirectoryName(sourcePath), document.TransientEntries[index]);
                string transientIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, transientPath);
                if (transientIdentity == "missing") {
                    continue;
                }
                if (!string.Equals(transientIdentity, document.ExpectedDestinationIdentity, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"The published authoring mutation '{journalPath}' found a changed quarantine entry.");
                }
                EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, transientPath, document.ExpectedDestinationIdentity);
            }
            RetireDocument(root, journalPath);
        }

        static void RecoverStagedDocument(string root, string journalPath, MutationDocument document) {
            if (string.IsNullOrWhiteSpace(document.StagedRelativePath) || string.IsNullOrWhiteSpace(document.StagedExactHash)) {
                throw new InvalidDataException($"The staged authoring mutation '{journalPath}' has no staged payload proof.");
            }
            string operationDirectory = Path.GetDirectoryName(journalPath);
            string stagedPath = Path.GetFullPath(Path.Combine(operationDirectory, document.StagedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string stagedPrefix = Path.Combine(operationDirectory, "staged") + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!stagedPath.StartsWith(stagedPrefix, comparison)) {
                throw new InvalidDataException($"The staged authoring mutation '{journalPath}' escaped its staging directory.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(stagedPath, operationDirectory);
            string stagedHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, stagedPath);
            string stagedIdentityProof = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, stagedPath);
            if (stagedIdentityProof != "missing" &&
                !string.Equals(stagedIdentityProof, document.StagedIdentity, StringComparison.Ordinal) &&
                (!string.Equals(document.Kind, "copy", StringComparison.Ordinal) &&
                   string.Equals(stagedIdentityProof, document.ExpectedDestinationIdentity, StringComparison.Ordinal))) {
                throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' found a changed staged inode.");
            }
            string destinationPath = Path.Combine(root, document.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string destinationHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, destinationPath);
            if (string.Equals(destinationHash, document.StagedExactHash, StringComparison.Ordinal)) {
                // Existing-destination exchange leaves the former destination
                // inode at the fixed payload name on POSIX until the cleanup
                // step. Accept only that exact recorded identity; an
                // unexpected payload must remain a recovery blocker.
                string stagedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, stagedPath);
                if (stagedIdentity == "missing" ||
                    string.Equals(stagedIdentity, document.ExpectedDestinationIdentity, StringComparison.Ordinal)) {
                    if (stagedIdentity != "missing") {
                        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, stagedPath, document.ExpectedDestinationIdentity);
                    }
                    RetireDocument(root, journalPath);
                    return;
                }
                throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' found an unexpected former destination payload.");
            }
            if (!string.Equals(document.Kind, "copy", StringComparison.Ordinal) &&
                string.Equals(stagedHash, document.StagedExactHash, StringComparison.Ordinal) &&
                string.Equals(destinationHash, document.StagedExactHash, StringComparison.Ordinal)) {
                RetireDocument(root, journalPath);
                return;
            }
            if (string.Equals(stagedHash, document.StagedExactHash, StringComparison.Ordinal) && destinationHash == "missing") {
                EditorAuthoringMutationScope.FixedRenameNoReplace(root, stagedPath, destinationPath, document.StagedIdentity, document.ExpectedDestinationIdentity);
                RetireDocument(root, journalPath);
                return;
            }
            if (!string.Equals(document.Kind, "copy", StringComparison.Ordinal) &&
                string.Equals(stagedHash, document.StagedExactHash, StringComparison.Ordinal) &&
                destinationHash != "missing") {
                string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                if (destinationIdentity == document.ExpectedDestinationIdentity) {
                    EditorAuthoringMutationScope.FixedRenameExchange(
                        root,
                        stagedPath,
                        destinationPath,
                        document.StagedIdentity,
                        document.ExpectedDestinationIdentity);
                    EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, stagedPath, document.ExpectedDestinationIdentity);
                    RetireDocument(root, journalPath);
                    return;
                }
            }
            // A failed publication can leave the original destination and its
            // exact staged publication leaf side by side. Remove only that
            // verified staged leaf and retire the operation; an unexpected
            // destination remains an explicit recovery blocker.
            if (string.Equals(destinationHash, "missing", StringComparison.Ordinal) == false) {
                string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, destinationPath);
                if (string.Equals(destinationIdentity, document.ExpectedDestinationIdentity, StringComparison.Ordinal)) {
                    string destinationParent = Path.GetDirectoryName(destinationPath);
                    for (int index = 0; index < document.TransientEntries.Count; index++) {
                        string transientPath = Path.Combine(destinationParent, document.TransientEntries[index]);
                        string transientHash = EditorAuthoringMutationScope.TryGetVerifiedSha256(root, transientPath);
                        if (string.Equals(transientHash, document.StagedExactHash, StringComparison.Ordinal)) {
                            EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(root, transientPath, document.StagedIdentity);
                        } else if (transientHash != "missing") {
                            throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' found an unexpected publication leaf.");
                        }
                    }
                    RetireDocument(root, journalPath);
                    return;
                }
            }
            throw new InvalidOperationException($"The staged authoring mutation '{journalPath}' has an ambiguous destination state.");
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
            public string ExpectedDestinationIdentity { get; set; }
            public string ExpectedDestinationHash { get; set; }
            public string StagedRelativePath { get; set; }
            public string StagedExactHash { get; set; }

            public string StagedIdentity { get; set; }
            public string Phase { get; set; }
            public List<string> TransientEntries { get; set; }
        }
    }
}
