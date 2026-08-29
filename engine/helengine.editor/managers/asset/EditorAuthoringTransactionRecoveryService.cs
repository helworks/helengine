using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Recovers only current-format authoring transaction journals in the exact transaction root.
    /// </summary>
    internal static class EditorAuthoringTransactionRecoveryService {
        static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        public static void Recover(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string canonicalProjectRoot = Path.GetFullPath(projectRootPath);
            string transactionRoot = GetTransactionRoot(canonicalProjectRoot);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(canonicalProjectRoot);
            // Resolve inode-bound namespace operations before transaction journals,
            // identity indexing, or importer setup can observe a partially moved leaf.
            EditorAuthoringMutationJournal.Recover(canonicalProjectRoot);
            // A transaction constructor creates its directory, lease, and first
            // manifest while holding this same lock. Recheck after acquiring it
            // so recovery cannot observe an incomplete construction and return
            // before the constructor has published its journal.
            ValidateTransactionContainer(canonicalProjectRoot);
            if (!Directory.Exists(transactionRoot)) {
                if (EditorAuthoringTransactionPendingMarker.ReadForRecovery(canonicalProjectRoot) != null) {
                    throw new InvalidOperationException("An authoring transaction pending marker has no transaction journal.");
                }
                return;
            }

            ValidateNoReparsePath(transactionRoot, transactionRoot);
            EditorAuthoringTransactionPendingMarker.PendingMarker pendingMarker = EditorAuthoringTransactionPendingMarker.ReadForRecovery(canonicalProjectRoot);
            string[] allTransactionEntries = Directory.GetFileSystemEntries(transactionRoot, "*", SearchOption.TopDirectoryOnly);
            List<string> creatingDirectories = new List<string>();
            List<string> deletingDirectories = new List<string>();
            List<string> transactionDirectories = new List<string>();
            for (int directoryIndex = 0; directoryIndex < allTransactionEntries.Length; directoryIndex++) {
                string directory = allTransactionEntries[directoryIndex];
                string name = Path.GetFileName(directory);
                ValidateNoReparsePath(directory, transactionRoot);
                if ((File.GetAttributes(directory) & FileAttributes.Directory) == 0) {
                    throw new InvalidDataException($"The authoring transaction root contains an unexpected file '{directory}'.");
                }
                if (name.StartsWith(".creating-", StringComparison.Ordinal)) {
                    ValidateTemporaryDirectoryName(name, ".creating-");
                    ValidateTreeHasNoReparsePoints(directory, transactionRoot);
                    creatingDirectories.Add(directory);
                } else if (name.StartsWith(".deleting-", StringComparison.Ordinal)) {
                    ValidateTemporaryDirectoryName(name, ".deleting-");
                    ValidateTreeHasNoReparsePoints(directory, transactionRoot);
                    deletingDirectories.Add(directory);
                } else {
                    transactionDirectories.Add(directory);
                }
            }
            // Validate the complete current transaction set before mutating any one entry.
            for (int preflightIndex = 0; preflightIndex < transactionDirectories.Count; preflightIndex++) {
                string preflightDirectory = transactionDirectories[preflightIndex];
                ValidateNoReparsePath(preflightDirectory, transactionRoot);
                string preflightId = Path.GetFileName(preflightDirectory);
                if (!Guid.TryParseExact(preflightId, "N", out _)) {
                    throw new InvalidDataException($"The authoring transaction directory '{preflightDirectory}' is not a current transaction id.");
                }
                ValidateTreeHasNoReparsePoints(preflightDirectory, transactionRoot);
                string preflightLease = ResolveContainedPath(preflightDirectory, "lease", "lease");
                if (!File.Exists(preflightLease)) {
                    throw new InvalidDataException($"The authoring transaction '{preflightDirectory}' has no lease.");
                }
                string preflightManifest = ResolveContainedPath(preflightDirectory, "transaction.json", "manifest");
                if (!File.Exists(preflightManifest)) {
                    throw new InvalidDataException($"The authoring transaction '{preflightDirectory}' has no journal.");
                }
                EditorAuthoringTransactionDocument preflightDocument = ReadDocument(preflightManifest, canonicalProjectRoot);
                ValidateDocument(preflightDocument, preflightId, preflightDirectory, canonicalProjectRoot);
                if (pendingMarker != null && string.Equals(preflightId, pendingMarker.TransactionId, StringComparison.Ordinal) &&
                    preflightDocument.State == EditorAuthoringTransactionState.Staging) {
                    throw new InvalidDataException("A pending transaction marker cannot identify a staging journal.");
                }
            }
            if (pendingMarker != null && !transactionDirectories.Any(directory =>
                string.Equals(Path.GetFileName(directory), pendingMarker.TransactionId, StringComparison.Ordinal))) {
                throw new InvalidOperationException("The authoring transaction pending marker does not identify a current transaction journal.");
            }
            // These namespaces are never published transaction state. Their
            // contents are safe to discard after the complete contained tree
            // has been validated, even when a crash cut left no journal/lease.
            for (int directoryIndex = 0; directoryIndex < creatingDirectories.Count; directoryIndex++) {
                DeleteTransactionDirectory(creatingDirectories[directoryIndex], transactionRoot);
            }
            for (int directoryIndex = 0; directoryIndex < deletingDirectories.Count; directoryIndex++) {
                DeleteTransactionDirectory(deletingDirectories[directoryIndex], transactionRoot);
            }
            bool pendingTransactionFound = pendingMarker == null;
            for (int index = 0; index < transactionDirectories.Count; index++) {
                string transactionDirectory = transactionDirectories[index];
                ValidateNoReparsePath(transactionDirectory, transactionRoot);
                string transactionId = Path.GetFileName(transactionDirectory);
                if (!Guid.TryParseExact(transactionId, "N", out _)) {
                    throw new InvalidDataException($"The authoring transaction directory '{transactionDirectory}' is not a current transaction id.");
                }

                ValidateTreeHasNoReparsePoints(transactionDirectory, transactionRoot);
                string manifestPath = Path.Combine(transactionDirectory, "transaction.json");
                if (!File.Exists(manifestPath)) {
                    throw new InvalidDataException($"The authoring transaction '{transactionDirectory}' has no journal.");
                }

                EditorAuthoringTransactionDocument document = ReadDocument(manifestPath, canonicalProjectRoot);
                ValidateDocument(document, transactionId, transactionDirectory, canonicalProjectRoot);
                if (pendingMarker != null && string.Equals(pendingMarker.TransactionId, transactionId, StringComparison.Ordinal)) {
                    pendingTransactionFound = true;
                    if (!pendingMarker.RelativePaths.All(path => document.Entries.Any(entry => string.Equals(entry.DestinationRelativePath, path, PathComparison)))) {
                        throw new InvalidDataException("The pending transaction marker does not match its journal.");
                    }
                    if (document.State == EditorAuthoringTransactionState.Staging) {
                        throw new InvalidDataException("A pending transaction marker cannot identify a staging journal.");
                    }
                }
                switch (document.State) {
                    case EditorAuthoringTransactionState.Staging:
                        if (TryAcquireAbandonedLease(transactionDirectory, canonicalProjectRoot)) {
                            DeleteTerminalTransactionDirectory(transactionDirectory, transactionRoot, transactionId);
                        }
                        break;
                    case EditorAuthoringTransactionState.Committing:
                    case EditorAuthoringTransactionState.Aborting:
                        if (!TryAcquireAbandonedLease(transactionDirectory, canonicalProjectRoot)) {
                            break;
                        }
                        using (pendingMarker != null && string.Equals(pendingMarker.TransactionId, transactionId, StringComparison.Ordinal)
                            ? EditorAuthoringTransactionPendingMarker.EnterOwner(canonicalProjectRoot, transactionId)
                            : null) {
                            bool rollbackPublished = EditorProjectWriteGeneration.HasRollbackPublicationUnderLock(
                                canonicalProjectRoot,
                                transactionId);
                            if (!rollbackPublished) {
                                IReadOnlyList<string> restoredPaths = Rollback(transactionDirectory, canonicalProjectRoot, document);
                                string[] restoredNativePaths = FilterNativeRestoredPaths(document, restoredPaths);
                                if (restoredNativePaths.Length > 0) {
                                    EditorProjectWriteGeneration.PublishRollbackChangesUnderLock(
                                        canonicalProjectRoot,
                                        transactionId,
                                        restoredNativePaths);
                                    rollbackPublished = true;
                                }
                            }
                            document.State = EditorAuthoringTransactionState.RolledBack;
                            for (int entryIndex = 0; entryIndex < document.Entries.Count; entryIndex++) {
                                document.Entries[entryIndex].State = document.State;
                            }
                            WriteDocumentAtomically(manifestPath, transactionDirectory, document);
                            if (pendingMarker != null && string.Equals(pendingMarker.TransactionId, transactionId, StringComparison.Ordinal)) {
                                EditorAuthoringTransactionPendingMarker.ClearUnderLock(canonicalProjectRoot, transactionId);
                            }
                            if (rollbackPublished || EditorProjectWriteGeneration.HasRollbackPublicationUnderLock(canonicalProjectRoot, transactionId)) {
                                EditorProjectWriteGeneration.PruneRollbackChangesUnderLock(canonicalProjectRoot, transactionId);
                            }
                        }
                        DeleteTerminalTransactionDirectory(transactionDirectory, transactionRoot, transactionId);
                        break;
                    case EditorAuthoringTransactionState.Committed:
                        if (pendingMarker != null && string.Equals(pendingMarker.TransactionId, transactionId, StringComparison.Ordinal)) {
                            EditorAuthoringTransactionPendingMarker.ClearUnderLock(canonicalProjectRoot, transactionId);
                        }
                        if (TryAcquireAbandonedLease(transactionDirectory, canonicalProjectRoot)) {
                            DeleteTerminalTransactionDirectory(transactionDirectory, transactionRoot, transactionId);
                        }
                        break;
                    case EditorAuthoringTransactionState.RolledBack:
                        if (pendingMarker != null && string.Equals(pendingMarker.TransactionId, transactionId, StringComparison.Ordinal)) {
                            EditorAuthoringTransactionPendingMarker.ClearUnderLock(canonicalProjectRoot, transactionId);
                        }
                        if (EditorProjectWriteGeneration.HasRollbackPublicationUnderLock(canonicalProjectRoot, transactionId)) {
                            EditorProjectWriteGeneration.PruneRollbackChangesUnderLock(canonicalProjectRoot, transactionId);
                        }
                        if (TryAcquireAbandonedLease(transactionDirectory, canonicalProjectRoot)) {
                            DeleteTerminalTransactionDirectory(transactionDirectory, transactionRoot, transactionId);
                        }
                        break;
                    default:
                        throw new InvalidDataException($"The authoring transaction '{manifestPath}' has an unsupported state.");
                }
            }
            if (!pendingTransactionFound) {
                throw new InvalidOperationException("The authoring transaction pending marker does not identify a current transaction journal.");
            }
        }

        internal static string GetTransactionRoot(string projectRootPath) {
            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "editor", "authoring-transactions");
        }

        internal static void ValidateTransactionContainer(string projectRootPath) {
            string projectRoot = Path.GetFullPath(projectRootPath);
            string assetsRoot = Path.Combine(projectRoot, "assets");
            string cacheRoot = Path.Combine(projectRoot, "cache");
            string editorRoot = Path.Combine(cacheRoot, "editor");
            string transactionRoot = GetTransactionRoot(projectRoot);
            ValidateNoReparsePath(projectRoot, projectRoot);
            ValidateNoReparsePath(assetsRoot, projectRoot);
            ValidateNoReparsePath(cacheRoot, projectRoot);
            ValidateNoReparsePath(editorRoot, projectRoot);
            ValidateNoReparsePath(transactionRoot, projectRoot);
        }

        internal static string ResolveContainedPath(string rootPath, string relativePath, string description) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                throw new InvalidDataException($"The authoring transaction {description} path is not relative.");
            }

            if (relativePath.IndexOf('\\') >= 0 || relativePath.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0) {
                throw new InvalidDataException($"The authoring transaction {description} path is not canonical.");
            }

            string canonicalRoot = Path.GetFullPath(rootPath);
            string rootName = Path.GetPathRoot(canonicalRoot);
            if (!string.Equals(canonicalRoot, rootName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                canonicalRoot = canonicalRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            string fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = canonicalRoot.EndsWith(Path.DirectorySeparatorChar) || canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar)
                ? canonicalRoot
                : canonicalRoot + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison) || string.Equals(fullPath, canonicalRoot, comparison)) {
                throw new InvalidDataException($"The authoring transaction {description} path escapes its containing root.");
            }

            string normalizedRelativePath = Path.GetRelativePath(canonicalRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            if (!string.Equals(normalizedRelativePath, relativePath, StringComparison.Ordinal)) {
                throw new InvalidDataException($"The authoring transaction {description} path is not canonical.");
            }
            if (string.Equals(description, "staged", StringComparison.Ordinal) &&
                !normalizedRelativePath.StartsWith("staged/", StringComparison.Ordinal)) {
                throw new InvalidDataException("The authoring transaction staged path is outside staged/.");
            }
            if (string.Equals(description, "backup", StringComparison.Ordinal) &&
                !normalizedRelativePath.StartsWith("backups/", StringComparison.Ordinal)) {
                throw new InvalidDataException("The authoring transaction backup path is outside backups/.");
            }

            ValidateNoReparsePath(fullPath, canonicalRoot);
            return fullPath;
        }

        static EditorAuthoringTransactionDocument ReadDocument(string manifestPath, string projectRootPath) {
            try {
                EditorAuthoringTransactionDocument document = JsonSerializer.Deserialize<EditorAuthoringTransactionDocument>(
                    EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, manifestPath),
                    EditorAuthoringTransactionDocument.JsonOptions);
                return document ?? throw new InvalidDataException($"The authoring transaction journal '{manifestPath}' is empty.");
            } catch (JsonException exception) {
                throw new InvalidDataException($"The authoring transaction journal '{manifestPath}' is malformed.", exception);
            }
        }

        static void WriteDocumentAtomically(
            string manifestPath,
            string transactionDirectory,
            EditorAuthoringTransactionDocument document) {
            string transactionRoot = Directory.GetParent(Path.GetFullPath(transactionDirectory))?.FullName;
            if (string.IsNullOrWhiteSpace(transactionRoot)) {
                throw new InvalidDataException("The authoring transaction has no transaction root.");
            }
            string projectRootPath = GetProjectRootFromTransactionRoot(transactionRoot);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, EditorAuthoringTransactionDocument.JsonOptions);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(projectRootPath, manifestPath, bytes);
        }

        static void ValidateDocument(
            EditorAuthoringTransactionDocument document,
            string transactionId,
            string transactionDirectory,
            string projectRootPath) {
            if (document.Version != EditorAuthoringTransactionDocument.CurrentVersion ||
                !string.Equals(document.TransactionId, transactionId, StringComparison.Ordinal) ||
                !Enum.IsDefined(document.State) ||
                document.Entries == null) {
                throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' has an unsupported shape.");
            }

            HashSet<string> destinations = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            string assetsRoot = Path.Combine(projectRootPath, "assets");
            for (int index = 0; index < document.Entries.Count; index++) {
                EditorAuthoringTransactionEntry entry = document.Entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.DestinationRelativePath) ||
                    !Enum.IsDefined(entry.State) ||
                    !Enum.IsDefined(entry.Progress) ||
                    !Enum.IsDefined(entry.PayloadKind) ||
                    !destinations.Add(entry.DestinationRelativePath)) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains duplicate or empty destinations.");
                }
                if ((!entry.Changed && entry.Progress != EditorAuthoringTransactionEntryProgress.Skipped) ||
                    (entry.Changed && entry.Progress == EditorAuthoringTransactionEntryProgress.Skipped)) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains invalid entry progress.");
                }

                string destinationRoot = entry.UsesProjectRoot ? projectRootPath : assetsRoot;
                string destination = ResolveContainedPath(destinationRoot, entry.DestinationRelativePath, "destination");
                string normalizedDestination = Path.GetRelativePath(destinationRoot, destination)
                    .Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                if (!string.Equals(normalizedDestination, entry.DestinationRelativePath, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.StagedRelativePath) ||
                    entry.State != document.State) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains a non-canonical entry.");
                }

                string stagedPath = ResolveContainedPath(transactionDirectory, entry.StagedRelativePath, "staged");
                string backupPath = null;
                if (!string.IsNullOrWhiteSpace(entry.BackupRelativePath)) {
                    backupPath = ResolveContainedPath(transactionDirectory, entry.BackupRelativePath, "backup");
                }

                // A marker-free staging journal has not entered publication. Its
                // contained tree may be discarded after path validation without
                // trusting incomplete payload or backup proofs.
                if (document.State == EditorAuthoringTransactionState.Staging) {
                    continue;
                }

                if (!File.Exists(stagedPath)) {
                    if (document.State != EditorAuthoringTransactionState.Committed) {
                        throw new InvalidDataException($"The authoring transaction staged payload '{stagedPath}' is missing.");
                    }
                } else {
                    byte[] stagedBytes = EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, stagedPath);
                    ValidateStagedPayload(entry, stagedBytes, destination);
                }

                if (entry.PriorExists) {
                    if (!IsValidHash(entry.PriorContentHash) || !IsValidHash(entry.PriorSerializedHash) ||
                        (RequiresAssetIdentity(entry) &&
                         (string.IsNullOrWhiteSpace(entry.ExpectedAssetId) || string.IsNullOrWhiteSpace(entry.ExpectedAssetKind)))) {
                        throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' is missing prior destination data.");
                    }
                    if (entry.Changed) {
                        if (string.IsNullOrWhiteSpace(entry.BackupRelativePath)) {
                            throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' is missing prior destination data.");
                        }
                        if (document.State == EditorAuthoringTransactionState.Committing && !File.Exists(backupPath)) {
                            throw new InvalidDataException($"The authoring transaction backup '{backupPath}' is missing.");
                        }
                        if (File.Exists(backupPath)) {
                            byte[] backupBytes = EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, backupPath);
                            if (!IsValidHash(entry.BackupContentHash) || !IsValidHash(entry.BackupSerializedHash)) {
                                throw new InvalidDataException($"The authoring transaction backup '{backupPath}' is missing exact integrity data.");
                            }
                            ValidateBackupPayload(entry, backupBytes, destination);
                        }
                    }
                } else if (!string.IsNullOrWhiteSpace(entry.BackupRelativePath) ||
                    !string.IsNullOrWhiteSpace(entry.PriorContentHash) || !string.IsNullOrWhiteSpace(entry.PriorSerializedHash)) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains an unexpected backup.");
                }
            }
        }

        static bool RequiresAssetIdentity(EditorAuthoringTransactionEntry entry) {
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile ||
                entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride) {
                return false;
            }
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                entry.IsMaterialSettingsPayload) {
                return entry.UpdatesIdentityIndex;
            }
            return true;
        }

        static string[] FilterNativeRestoredPaths(
            EditorAuthoringTransactionDocument document,
            IReadOnlyList<string> restoredPaths) {
            if (document == null || restoredPaths == null || restoredPaths.Count == 0) {
                return Array.Empty<string>();
            }

            HashSet<string> restored = new HashSet<string>(restoredPaths, OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            return document.Entries
                .Where(entry => !entry.UsesProjectRoot && restored.Contains(entry.DestinationRelativePath))
                .Select(entry => entry.DestinationRelativePath)
                .ToArray();
        }

        static void ValidateStagedPayload(
            EditorAuthoringTransactionEntry entry,
            byte[] bytes,
            string destination) {
            if (!IsValidHash(entry.StagedContentHash) || !IsValidHash(entry.StagedSerializedHash) ||
                (RequiresAssetIdentity(entry) &&
                 (string.IsNullOrWhiteSpace(entry.ExpectedAssetId) || string.IsNullOrWhiteSpace(entry.ExpectedAssetKind)))) {
                throw new InvalidDataException($"The authoring transaction staged payload '{destination}' is missing exact integrity data.");
            }

            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile) {
                EditorNativeAssetWriteService.ValidateGeneratedFilePayload(bytes, entry.StagedContentHash, entry.ExpectedAssetKind);
            } else if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                       entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride ||
                       entry.IsMaterialSettingsPayload) {
                bool commonDocument = entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                    (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.NativeAsset && entry.IsMaterialSettingsPayload && entry.UpdatesIdentityIndex);
                EditorNativeAssetWriteService.ValidateMaterialSettingsPayload(bytes, destination, commonDocument);
                string actualSerializedHash = ComputeSerializedHash(bytes);
                if (!string.Equals(actualSerializedHash, entry.StagedSerializedHash, StringComparison.Ordinal)) {
                    throw new InvalidDataException("The staged material-settings payload does not match its journal.");
                }
                string actualContentHash = commonDocument
                    ? EditorNativeAssetWriteService.ComputeCanonicalMaterialSettingsHash(bytes)
                    : actualSerializedHash;
                if (!string.Equals(actualContentHash, entry.StagedContentHash, StringComparison.Ordinal)) {
                    throw new InvalidDataException("The staged material-settings content hash does not match its journal.");
                }
                if (commonDocument) {
                    using MemoryStream stream = new MemoryStream(bytes, writable: false);
                    MaterialAssetCommonSettingsDocument document = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream);
                    if (!string.Equals(document.AuthoringAssetId, entry.ExpectedAssetId, StringComparison.Ordinal)) {
                        throw new InvalidDataException("The staged material-settings identity does not match its journal.");
                    }
                }
            } else {
                EditorNativeAssetWriteService.ValidateNativePayloadIntegrity(
                    bytes,
                    destination,
                    entry.StagedContentHash,
                    entry.StagedSerializedHash,
                    entry.ExpectedAssetId,
                    entry.ExpectedAssetKind);
            }
        }

        static void ValidateBackupPayload(
            EditorAuthoringTransactionEntry entry,
            byte[] bytes,
            string destination) {
            string contentHash = entry.BackupContentHash ?? entry.PriorContentHash;
            string serializedHash = entry.BackupSerializedHash ?? entry.PriorSerializedHash;
            if (!IsValidHash(contentHash) || !IsValidHash(serializedHash)) {
                throw new InvalidDataException("The transaction backup is missing exact integrity data.");
            }
            if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.GeneratedFile) {
                EditorNativeAssetWriteService.ValidateGeneratedFilePayload(bytes, contentHash, entry.ExpectedAssetKind);
            } else if (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                       entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialPlatformOverride ||
                       entry.IsMaterialSettingsPayload) {
                bool commonDocument = entry.PayloadKind == EditorAuthoringTransactionPayloadKind.MaterialCommonSettings ||
                    (entry.PayloadKind == EditorAuthoringTransactionPayloadKind.NativeAsset && entry.IsMaterialSettingsPayload && entry.UpdatesIdentityIndex);
                EditorNativeAssetWriteService.ValidateMaterialSettingsPayload(bytes, destination, commonDocument);
                string actualSerializedHash = ComputeSerializedHash(bytes);
                string actualContentHash = commonDocument
                    ? EditorNativeAssetWriteService.ComputeCanonicalMaterialSettingsHash(bytes)
                    : actualSerializedHash;
                if (!string.Equals(actualSerializedHash, serializedHash, StringComparison.Ordinal) ||
                    !string.Equals(actualContentHash, contentHash, StringComparison.Ordinal)) {
                    throw new InvalidDataException("The transaction backup material payload does not match its journal.");
                }
            } else {
                EditorNativeAssetWriteService.ValidateNativePayloadIntegrity(
                    bytes,
                    destination,
                    contentHash,
                    serializedHash,
                    entry.ExpectedAssetId,
                    entry.ExpectedAssetKind);
            }
        }

        static bool IsValidHash(string hash) {
            if (string.IsNullOrWhiteSpace(hash) || !hash.StartsWith("sha256:", StringComparison.Ordinal) || hash.Length != 71) {
                return false;
            }
            for (int index = 7; index < hash.Length; index++) {
                char character = hash[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }

        static IReadOnlyList<string> Rollback(string transactionDirectory, string projectRootPath, EditorAuthoringTransactionDocument document) {
            string assetsRoot = Path.Combine(projectRootPath, "assets");
            List<Exception> failures = new List<Exception>();
            List<string> restoredPaths = new List<string>();
            List<RollbackOperation> operations = new List<RollbackOperation>();

            // Complete every proof before changing any destination. A later
            // divergent path must not leave an earlier path partially restored.
            for (int index = 0; index < document.Entries.Count; index++) {
                EditorAuthoringTransactionEntry entry = document.Entries[index];
                if (!entry.Changed || entry.Progress == EditorAuthoringTransactionEntryProgress.Staged ||
                    entry.Progress == EditorAuthoringTransactionEntryProgress.Skipped) {
                    continue;
                }

                string destinationRoot = entry.UsesProjectRoot ? projectRootPath : assetsRoot;
                string destination = ResolveContainedPath(destinationRoot, entry.DestinationRelativePath, "destination");
                bool replacementApplied = IsReplacementApplied(destination, destinationRoot, projectRootPath, entry);
                if (!replacementApplied) {
                    // The entry was durably marked as applying/applied. Include
                    // it in the restored generation even when the destination
                    // already contains its prior bytes, so observers replay
                    // every path across a crash cut between restore and publish.
                    restoredPaths.Add(entry.DestinationRelativePath);
                    continue;
                }

                byte[] backupBytes = null;
                if (entry.PriorExists) {
                    string backup = ResolveContainedPath(transactionDirectory, entry.BackupRelativePath, "backup");
                    backupBytes = EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, backup);
                    ValidateBackup(backupBytes, destination, entry);
                }
                operations.Add(new RollbackOperation(entry, destination, backupBytes));
            }

            // The operation phase is intentionally separate from the proof
            // phase. If one replacement fails, continue restoring every other
            // already-proven entry and retain all failures for the caller.
            for (int index = operations.Count - 1; index >= 0; index--) {
                RollbackOperation operation = operations[index];
                try {
                    // An external edit after preflight is never overwritten.
                    // A prior-byte restoration by another recovery attempt is
                    // already safe and needs no second replacement.
                    string destinationRoot = operation.Entry.UsesProjectRoot ? projectRootPath : assetsRoot;
                    if (!IsReplacementApplied(operation.Destination, destinationRoot, projectRootPath, operation.Entry)) {
                        restoredPaths.Add(operation.Entry.DestinationRelativePath);
                        continue;
                    }
                    if (operation.Entry.PriorExists) {
                        ReplaceAtomically(operation.Destination, operation.BackupBytes, destinationRoot, projectRootPath);
                    } else if (File.Exists(operation.Destination)) {
                        EditorAuthoringMutationScope.DeleteLeaf(projectRootPath, operation.Destination);
                    }
                    restoredPaths.Add(operation.Entry.DestinationRelativePath);
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0) {
                throw new AggregateException("Authoring transaction recovery could not restore every destination.", failures);
            }
            return restoredPaths;
        }

        sealed class RollbackOperation {
            public RollbackOperation(EditorAuthoringTransactionEntry entry, string destination, byte[] backupBytes) {
                Entry = entry;
                Destination = destination;
                BackupBytes = backupBytes;
            }

            public EditorAuthoringTransactionEntry Entry { get; }

            public string Destination { get; }

            public byte[] BackupBytes { get; }
        }

        static bool IsReplacementApplied(
            string destination,
            string destinationRoot,
            string projectRootPath,
            EditorAuthoringTransactionEntry entry) {
            ValidateNoReparsePath(destination, destinationRoot);
            if (!File.Exists(destination)) {
                if (entry.PriorExists) {
                    throw new InvalidDataException($"The transaction destination '{destination}' disappeared before recovery.");
                }
                return false;
            }

            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidDataException("The authoring transaction has no project parent.");
            }
            byte[] currentBytes = EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, destination);
            string currentHash = ComputeSerializedHash(currentBytes);
            if (string.Equals(currentHash, entry.StagedSerializedHash, StringComparison.Ordinal)) {
                return true;
            }
            if (entry.PriorExists && string.Equals(currentHash, entry.PriorSerializedHash, StringComparison.Ordinal)) {
                return false;
            }
            throw new InvalidDataException($"The transaction destination '{destination}' diverged after the process stopped.");
        }

        static void ValidateBackup(byte[] bytes, string destination, EditorAuthoringTransactionEntry entry) {
            ValidateBackupPayload(entry, bytes, destination);
        }

        static string ComputeSerializedHash(byte[] bytes) {
            return string.Concat("sha256:", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
        }

        static bool TryAcquireAbandonedLease(string transactionDirectory, string projectRootPath) {
            string leasePath = ResolveContainedPath(transactionDirectory, "lease", "lease");
            try {
                if (!File.Exists(leasePath)) {
                    throw new InvalidDataException($"The authoring transaction '{transactionDirectory}' has no lease.");
                }
                using EditorAuthoringMutationScope mutationScope = EditorAuthoringMutationScope.AcquireForMutation(
                    projectRootPath,
                    transactionDirectory);
                using EditorAuthoringVerifiedFile lease = mutationScope.OpenVerifiedFile(
                    leasePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return mutationScope.TryAcquireExclusiveFileLock(lease);
            } catch (IOException) {
                return false;
            } catch (UnauthorizedAccessException) {
                return false;
            }
        }

        internal static void ReplaceAtomically(
            string destinationPath,
            byte[] bytes,
            string containingRoot,
            string projectRootPath) {
            ValidateNoReparsePath(destinationPath, containingRoot);
            string canonicalProjectRootPath = Path.GetFullPath(projectRootPath);
            if (string.IsNullOrWhiteSpace(canonicalProjectRootPath)) {
                throw new InvalidDataException("The authoring mutation root has no project parent.");
            }
            ValidateNoReparsePath(canonicalProjectRootPath, canonicalProjectRootPath);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(canonicalProjectRootPath, destinationPath, bytes);
        }

        static void DeleteTransactionDirectory(string transactionDirectory, string transactionRoot) {
            string projectRootPath = GetProjectRootFromTransactionRoot(transactionRoot);
            ValidateTreeHasNoReparsePoints(transactionDirectory, transactionRoot);
            EditorAuthoringMutationScope.DeleteDirectoryTree(projectRootPath, transactionDirectory, transactionRoot);
        }

        static void DeleteTerminalTransactionDirectory(string transactionDirectory, string transactionRoot, string transactionId) {
            if (!Directory.Exists(transactionDirectory)) {
                return;
            }

            string projectRootPath = GetProjectRootFromTransactionRoot(transactionRoot);
            ValidateTreeHasNoReparsePoints(transactionDirectory, transactionRoot);
            string deletingDirectory = ResolveContainedPath(
                transactionRoot,
                ".deleting-" + transactionId,
                "terminal deletion");
            ValidateNoReparsePath(deletingDirectory, transactionRoot);
            EditorAuthoringMutationScope.MoveDirectory(projectRootPath, transactionDirectory, deletingDirectory);
            ValidateTreeHasNoReparsePoints(deletingDirectory, transactionRoot);
            EditorAuthoringMutationScope.DeleteDirectoryTree(projectRootPath, deletingDirectory, transactionRoot);
        }

        static string GetProjectRootFromTransactionRoot(string transactionRoot) {
            DirectoryInfo transactionRootInfo = new DirectoryInfo(Path.GetFullPath(transactionRoot));
            DirectoryInfo editorDirectory = transactionRootInfo.Parent;
            DirectoryInfo cacheDirectory = editorDirectory?.Parent;
            DirectoryInfo projectDirectory = cacheDirectory?.Parent;
            if (projectDirectory == null) {
                throw new InvalidDataException($"The authoring transaction root '{transactionRoot}' has no project parent.");
            }
            return projectDirectory.FullName;
        }

        static void ValidateTemporaryDirectoryName(string name, string prefix) {
            string id = name.Substring(prefix.Length);
            if (!Guid.TryParseExact(id, "N", out _)) {
                throw new InvalidDataException($"The authoring transaction temporary directory '{name}' is not a current transaction namespace.");
            }
        }

        internal static void ValidateTreeHasNoReparsePoints(string path, string containingRoot) {
            Stack<string> pendingDirectories = new Stack<string>();
            pendingDirectories.Push(path);
            while (pendingDirectories.Count > 0) {
                string currentDirectory = pendingDirectories.Pop();
                ValidateNoReparsePath(currentDirectory, containingRoot);
                foreach (string child in Directory.EnumerateFileSystemEntries(currentDirectory, "*", SearchOption.TopDirectoryOnly)) {
                    // Inspect the child before deciding whether to recurse. A
                    // recursive enumeration can follow a linked directory
                    // before its entry is validated, which would make a
                    // supposedly contained cleanup walk outside the project.
                    ValidateNoReparsePath(child, containingRoot);
                    if ((File.GetAttributes(child) & FileAttributes.Directory) != 0) {
                        pendingDirectories.Push(child);
                    }
                }
            }
        }

        internal static void ValidateNoReparsePath(string path, string containingRoot) {
            string canonicalRoot = Path.GetFullPath(containingRoot);
            string rootName = Path.GetPathRoot(canonicalRoot);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(canonicalRoot, rootName, comparison)) {
                canonicalRoot = canonicalRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            string current = Path.GetFullPath(path);
            string prefix = canonicalRoot.EndsWith(Path.DirectorySeparatorChar) || canonicalRoot.EndsWith(Path.AltDirectorySeparatorChar)
                ? canonicalRoot
                : canonicalRoot + Path.DirectorySeparatorChar;
            if (!string.Equals(current, canonicalRoot, comparison) && !current.StartsWith(prefix, comparison)) {
                throw new InvalidDataException($"The authoring transaction path '{path}' escapes its containing root.");
            }

            while (true) {
                try {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidDataException($"The authoring transaction path '{path}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }
                // Continue through the complete existing ancestor chain. A
                // linked parent above the textual project root can redirect a
                // seemingly contained path outside the project.
                if (string.Equals(current, Path.GetPathRoot(canonicalRoot), comparison)) {
                    break;
                }
                string parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent)) {
                    break;
                }
                current = parent;
            }
        }
    }
}
