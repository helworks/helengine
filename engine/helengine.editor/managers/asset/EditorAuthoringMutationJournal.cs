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
        static readonly AsyncLocal<EditorAuthoringMutationJournal> Current = new AsyncLocal<EditorAuthoringMutationJournal>();
        readonly string ProjectRootPath;
        readonly string JournalPath;
        readonly MutationDocument Document;
        readonly EditorAuthoringMutationJournal PreviousCurrent;
        readonly bool Ephemeral;
        bool Completed;
        int TransientSequence;

        EditorAuthoringMutationJournal(string projectRootPath, string journalPath, MutationDocument document) {
            ProjectRootPath = projectRootPath;
            JournalPath = journalPath;
            Document = document;
        }

        EditorAuthoringMutationJournal(string projectRootPath, EditorAuthoringMutationJournal previousCurrent) {
            ProjectRootPath = projectRootPath;
            PreviousCurrent = previousCurrent;
            Ephemeral = true;
            Document = new MutationDocument {
                OperationId = Guid.NewGuid().ToString("N"),
                Phase = "Prepared",
                TransientEntries = new List<string>()
            };
        }

        internal static IDisposable EnterEphemeral(string projectRootPath) {
            EditorAuthoringMutationJournal operation = new EditorAuthoringMutationJournal(projectRootPath, Current.Value);
            Current.Value = operation;
            return operation;
        }

        internal static EditorAuthoringMutationJournal Begin(string projectRootPath, string kind, string sourcePath, string destinationPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            string root = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(root, root);
            string sourceRelativePath = NormalizeRelativePath(root, sourcePath);
            string destinationRelativePath = NormalizeRelativePath(root, destinationPath);
            string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
            EditorAuthoringMutationScope.EnsureDirectory(root, journalDirectory);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalDirectory, root);
            string operationId = Guid.NewGuid().ToString("N");
            string journalPath = Path.Combine(journalDirectory, operationId + ".json");
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalPath, journalDirectory);
            MutationDocument document = new MutationDocument {
                Version = CurrentVersion,
                OperationId = operationId,
                Kind = kind ?? string.Empty,
                SourceRelativePath = sourceRelativePath,
                DestinationRelativePath = destinationRelativePath,
                ExpectedSourceIdentity = CaptureIdentity(root, sourcePath),
                ExpectedDestinationIdentity = CaptureIdentity(root, destinationPath),
                Phase = "Prepared",
                TransientEntries = new List<string>()
            };
            WriteDocument(journalPath, document, root, createNew: true);
            EditorAuthoringMutationJournal journal = new EditorAuthoringMutationJournal(root, journalPath, document);
            Current.Value = journal;
            return journal;
        }

        internal static string ReserveTransientName(string originalName) {
            EditorAuthoringMutationJournal journal = Current.Value;
            if (journal == null) {
                return ".authoring-mutation-untracked-" + Guid.NewGuid().ToString("N");
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
            Current.Value?.MarkPhase(phase);
        }

        internal static void SetCurrentExpectedIdentities(string sourceIdentity, string destinationIdentity = null) {
            EditorAuthoringMutationJournal journal = Current.Value;
            if (journal == null || journal.Completed) {
                return;
            }
            journal.Document.ExpectedSourceIdentity = sourceIdentity ?? "unknown";
            if (destinationIdentity != null) {
                journal.Document.ExpectedDestinationIdentity = destinationIdentity;
            }
            journal.Persist();
        }

        internal void Complete() {
            if (Completed) {
                return;
            }
            Document.Phase = "Completed";
            Persist();
            Completed = true;
            if (ReferenceEquals(Current.Value, this)) {
                Current.Value = null;
            }
            // The completed document is a recoverable cleanup marker. A failed
            // retirement must not make a successful namespace mutation appear
            // unsuccessful or re-enter this journal while deleting itself.
            try {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(JournalPath, ProjectRootPath);
                EditorAuthoringMutationScope.DeleteLeafWithoutJournal(ProjectRootPath, JournalPath);
            } catch {
                // Startup recovery removes completed documents after validating
                // their contained journal path.
            }
        }

        internal static void Recover(string projectRootPath) {
            string root = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(root, root);
            string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
            if (!Directory.Exists(journalDirectory)) {
                return;
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(journalDirectory, root);
            string[] files = Directory.GetFiles(journalDirectory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string path in files) {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, journalDirectory);
                MutationDocument document;
                try {
                    document = JsonSerializer.Deserialize<MutationDocument>(
                        Encoding.UTF8.GetString(EditorAuthoringMutationScope.ReadAllBytes(root, path)));
                } catch (Exception exception) {
                    throw new InvalidDataException($"The authoring mutation journal '{path}' is malformed.", exception);
                }
                ValidateDocument(document, path, root);
                if (string.Equals(document.Phase, "Prepared", StringComparison.Ordinal)) {
                    EditorAuthoringMutationScope.DeleteLeafWithoutJournal(root, path);
                    continue;
                }
                if (string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal)) {
                    string sourcePath = Path.Combine(root, document.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
                    string sourceParent = Path.GetDirectoryName(sourcePath);
                    for (int transientIndex = 0; transientIndex < document.TransientEntries.Count; transientIndex++) {
                        string transientPath = Path.Combine(sourceParent, document.TransientEntries[transientIndex]);
                        if (File.Exists(transientPath) && !File.Exists(sourcePath)) {
                            EditorAuthoringMutationScope.MoveLeaf(root, transientPath, sourcePath);
                        }
                    }
                    if (File.Exists(sourcePath)) {
                        EditorAuthoringMutationScope.DeleteLeafWithoutJournal(root, path);
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
        }

        static void WriteDocument(string path, MutationDocument document, string root, bool createNew) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, Path.GetDirectoryName(path));
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, new JsonSerializerOptions { WriteIndented = false });
            EditorAuthoringMutationScope.WriteAllBytesAtomicallyWithoutJournal(root, path, bytes, !createNew);
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

        static void ValidateDocument(MutationDocument document, string path, string root) {
            if (document == null || document.Version != CurrentVersion || string.IsNullOrWhiteSpace(document.OperationId) ||
                !Guid.TryParseExact(document.OperationId, "N", out _) || string.IsNullOrWhiteSpace(document.Kind) ||
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
                !string.Equals(document.Phase, "Quarantining", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Published", StringComparison.Ordinal) &&
                !string.Equals(document.Phase, "Completed", StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring mutation journal '{path}' contains an unsupported phase '{document.Phase}'.");
            }
            string journalDirectory = Path.Combine(root, "cache", "editor", JournalDirectoryName);
            string expectedPrefix = Path.GetFullPath(journalDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(expectedPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
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
                if (destinationIdentity != "missing") {
                    throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found an unexpected destination entry.");
                }
                for (int index = 0; index < document.TransientEntries.Count; index++) {
                    string transientPath = Path.Combine(Path.GetDirectoryName(sourcePath), document.TransientEntries[index]);
                    string transientIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(root, transientPath);
                    if (transientIdentity == "missing") {
                        continue;
                    }
                    if (!string.Equals(transientIdentity, document.ExpectedSourceIdentity, StringComparison.Ordinal)) {
                        throw new InvalidOperationException($"The published authoring deletion '{journalPath}' found a changed quarantine entry.");
                    }
                    EditorAuthoringMutationScope.DeleteLeafWithoutJournal(root, transientPath);
                }
                RetireDocument(root, journalPath);
                return;
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
                EditorAuthoringMutationScope.DeleteLeafWithoutJournal(root, transientPath);
            }
            RetireDocument(root, journalPath);
        }

        static void RetireDocument(string root, string journalPath) {
            EditorAuthoringMutationScope.DeleteLeafWithoutJournal(root, journalPath);
        }

        sealed class MutationDocument {
            public int Version { get; set; }
            public string OperationId { get; set; }
            public string Kind { get; set; }
            public string SourceRelativePath { get; set; }
            public string DestinationRelativePath { get; set; }
            public string ExpectedSourceIdentity { get; set; }
            public string ExpectedDestinationIdentity { get; set; }
            public string Phase { get; set; }
            public List<string> TransientEntries { get; set; }
        }
    }
}
