using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Recovers only current-format authoring transaction journals in the exact transaction root.
    /// </summary>
    internal static class EditorAuthoringTransactionRecoveryService {
        public static void Recover(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string canonicalProjectRoot = Path.GetFullPath(projectRootPath);
            string transactionRoot = GetTransactionRoot(canonicalProjectRoot);
            if (!Directory.Exists(transactionRoot)) {
                return;
            }

            ValidateNoReparsePath(transactionRoot, transactionRoot);
            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(canonicalProjectRoot);
            string[] transactionDirectories = Directory.GetDirectories(transactionRoot, "*", SearchOption.TopDirectoryOnly);
            for (int index = 0; index < transactionDirectories.Length; index++) {
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

                EditorAuthoringTransactionDocument document = ReadDocument(manifestPath);
                ValidateDocument(document, transactionId, transactionDirectory, canonicalProjectRoot);
                switch (document.State) {
                    case EditorAuthoringTransactionState.Staging:
                        DeleteTransactionDirectory(transactionDirectory, transactionRoot);
                        break;
                    case EditorAuthoringTransactionState.Committing:
                        Rollback(transactionDirectory, canonicalProjectRoot, document);
                        DeleteTransactionDirectory(transactionDirectory, transactionRoot);
                        break;
                    case EditorAuthoringTransactionState.Committed:
                        DeleteTransactionDirectory(transactionDirectory, transactionRoot);
                        break;
                    default:
                        throw new InvalidDataException($"The authoring transaction '{manifestPath}' has an unsupported state.");
                }
            }
        }

        internal static string GetTransactionRoot(string projectRootPath) {
            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "editor", "authoring-transactions");
        }

        internal static string ResolveContainedPath(string rootPath, string relativePath, string description) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) {
                throw new InvalidDataException($"The authoring transaction {description} path is not relative.");
            }

            string canonicalRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = canonicalRoot + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison) || string.Equals(fullPath, canonicalRoot, comparison)) {
                throw new InvalidDataException($"The authoring transaction {description} path escapes its containing root.");
            }

            ValidateNoReparsePath(fullPath, canonicalRoot);
            return fullPath;
        }

        static EditorAuthoringTransactionDocument ReadDocument(string manifestPath) {
            try {
                EditorAuthoringTransactionDocument document = JsonSerializer.Deserialize<EditorAuthoringTransactionDocument>(
                    File.ReadAllText(manifestPath),
                    EditorAuthoringTransactionDocument.JsonOptions);
                return document ?? throw new InvalidDataException($"The authoring transaction journal '{manifestPath}' is empty.");
            } catch (JsonException exception) {
                throw new InvalidDataException($"The authoring transaction journal '{manifestPath}' is malformed.", exception);
            }
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
                    !destinations.Add(entry.DestinationRelativePath)) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains duplicate or empty destinations.");
                }

                string destination = ResolveContainedPath(assetsRoot, entry.DestinationRelativePath, "destination");
                string normalizedDestination = Path.GetRelativePath(assetsRoot, destination)
                    .Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                if (!string.Equals(normalizedDestination, entry.DestinationRelativePath, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.StagedRelativePath) ||
                    entry.State != document.State) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains a non-canonical entry.");
                }

                string stagedPath = ResolveContainedPath(transactionDirectory, entry.StagedRelativePath, "staged");
                if (!File.Exists(stagedPath)) {
                    if (document.State != EditorAuthoringTransactionState.Committed) {
                        throw new InvalidDataException($"The authoring transaction staged payload '{stagedPath}' is missing.");
                    }
                } else {
                    byte[] stagedBytes = File.ReadAllBytes(stagedPath);
                    EditorNativeAssetWriteService.ValidateCurrentNativePayload(stagedBytes, destination);
                    string stagedHash = EditorNativeAssetWriteService.ComputeCanonicalNativeHash(stagedBytes, destination);
                    if (!IsValidHash(entry.StagedContentHash) || !string.Equals(stagedHash, entry.StagedContentHash, StringComparison.Ordinal)) {
                        throw new InvalidDataException($"The authoring transaction staged payload '{stagedPath}' has an invalid content hash.");
                    }
                }
                if (!string.IsNullOrWhiteSpace(entry.BackupRelativePath)) {
                    ResolveContainedPath(transactionDirectory, entry.BackupRelativePath, "backup");
                }

                if (entry.PriorExists) {
                    if (!IsValidHash(entry.PriorContentHash) || !IsValidHash(entry.PriorSerializedHash)) {
                        throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' is missing prior destination data.");
                    }
                    if (entry.Changed) {
                        if (string.IsNullOrWhiteSpace(entry.BackupRelativePath)) {
                            throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' is missing prior destination data.");
                        }
                        string backupPath = ResolveContainedPath(transactionDirectory, entry.BackupRelativePath, "backup");
                        if (document.State == EditorAuthoringTransactionState.Committing && !File.Exists(backupPath)) {
                            throw new InvalidDataException($"The authoring transaction backup '{backupPath}' is missing.");
                        }
                        if (File.Exists(backupPath)) {
                            byte[] backupBytes = File.ReadAllBytes(backupPath);
                            EditorNativeAssetWriteService.ValidateCurrentNativePayload(backupBytes, destination);
                            if (!string.Equals(
                                    EditorNativeAssetWriteService.ComputeCanonicalNativeHash(backupBytes, destination),
                                    entry.PriorContentHash,
                                    StringComparison.Ordinal)) {
                                throw new InvalidDataException($"The authoring transaction backup '{backupPath}' does not match its prior content hash.");
                            }
                        }
                    }
                } else if (!string.IsNullOrWhiteSpace(entry.BackupRelativePath) ||
                    !string.IsNullOrWhiteSpace(entry.PriorContentHash) || !string.IsNullOrWhiteSpace(entry.PriorSerializedHash)) {
                    throw new InvalidDataException($"The authoring transaction journal '{transactionDirectory}' contains an unexpected backup.");
                }
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

        static void Rollback(string transactionDirectory, string projectRootPath, EditorAuthoringTransactionDocument document) {
            string assetsRoot = Path.Combine(projectRootPath, "assets");
            List<Exception> failures = new List<Exception>();
            for (int index = 0; index < document.Entries.Count; index++) {
                EditorAuthoringTransactionEntry entry = document.Entries[index];
                try {
                    if (!entry.Changed) {
                        continue;
                    }
                    string destination = ResolveContainedPath(assetsRoot, entry.DestinationRelativePath, "destination");
                    if (entry.PriorExists) {
                        string backup = ResolveContainedPath(transactionDirectory, entry.BackupRelativePath, "backup");
                        ReplaceAtomically(destination, File.ReadAllBytes(backup));
                    } else if (File.Exists(destination)) {
                        File.Delete(destination);
                    }
                } catch (Exception exception) {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0) {
                throw new AggregateException("Authoring transaction recovery could not restore every destination.", failures);
            }
        }

        internal static void ReplaceAtomically(string destinationPath, byte[] bytes) {
            string directoryPath = Path.GetDirectoryName(destinationPath);
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = Path.Combine(directoryPath, "." + Path.GetFileName(destinationPath) + ".restore-" + Guid.NewGuid().ToString("N"));
            try {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, destinationPath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }

        static void DeleteTransactionDirectory(string transactionDirectory, string transactionRoot) {
            ValidateTreeHasNoReparsePoints(transactionDirectory, transactionRoot);
            Directory.Delete(transactionDirectory, true);
        }

        static void ValidateTreeHasNoReparsePoints(string path, string containingRoot) {
            ValidateNoReparsePath(path, containingRoot);
            foreach (string child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)) {
                ValidateNoReparsePath(child, containingRoot);
            }
        }

        static void ValidateNoReparsePath(string path, string containingRoot) {
            string canonicalRoot = Path.GetFullPath(containingRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = Path.GetFullPath(path);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string prefix = canonicalRoot + Path.DirectorySeparatorChar;
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
                if (string.Equals(current, canonicalRoot, comparison)) {
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
