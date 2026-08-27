using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Durable blocker written while a native authoring transaction is publishing.
    /// </summary>
    internal static class EditorAuthoringTransactionPendingMarker {
        const int CurrentVersion = 1;
        [ThreadStatic]
        static HashSet<string> OwnedProjects;

        internal static string GetPath(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            return Path.Combine(Path.GetFullPath(projectRootPath), "cache", "editor", "authoring-transactions.pending");
        }

        internal static void EnsureNoPending(string projectRootPath) {
            string canonicalRoot = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(canonicalRoot);
            if (OwnedProjects != null && OwnedProjects.Contains(canonicalRoot)) {
                return;
            }

            string markerPath = GetPath(canonicalRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, canonicalRoot);
            if (File.Exists(markerPath)) {
                ReadAndValidate(markerPath, canonicalRoot);
                throw new InvalidOperationException($"Authoring transaction recovery is required for pending transaction '{markerPath}'.");
            }
        }

        internal static IDisposable EnterOwner(string projectRootPath, string transactionId) {
            string canonicalRoot = Path.GetFullPath(projectRootPath);
            string markerPath = GetPath(canonicalRoot);
            if (!File.Exists(markerPath)) {
                throw new InvalidOperationException("The authoring transaction pending marker is missing.");
            }

            PendingMarker marker = ReadAndValidate(markerPath, canonicalRoot);
            if (!string.Equals(marker.TransactionId, transactionId, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The authoring transaction pending marker belongs to another transaction.");
            }

            (OwnedProjects ??= new HashSet<string>(ProjectComparer)).Add(canonicalRoot);
            return new OwnerScope(canonicalRoot);
        }

        internal static void PublishUnderLock(string projectRootPath, string transactionId, IReadOnlyList<string> relativePaths) {
            string canonicalRoot = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(canonicalRoot);
            if (string.IsNullOrWhiteSpace(transactionId) || !Guid.TryParseExact(transactionId, "N", out _)) {
                throw new ArgumentException("A current transaction identifier is required.", nameof(transactionId));
            }
            if (relativePaths == null || relativePaths.Count == 0) {
                throw new ArgumentException("A pending transaction must contain paths.", nameof(relativePaths));
            }

            string markerPath = GetPath(canonicalRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, canonicalRoot);
            if (File.Exists(markerPath)) {
                PendingMarker current = ReadAndValidate(markerPath, canonicalRoot);
                if (!string.Equals(current.TransactionId, transactionId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException("Another authoring transaction is pending.");
                }
                return;
            }

            string assetsRoot = Path.Combine(canonicalRoot, "assets");
            List<string> paths = relativePaths
                .Select(path => NormalizeAssetPath(assetsRoot, path))
                .Distinct(PathComparer)
                .OrderBy(path => path, PathComparer)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToList();
            WriteAtomically(canonicalRoot, markerPath, new PendingMarker {
                Version = CurrentVersion,
                TransactionId = transactionId,
                RelativePaths = paths
            });
        }

        internal static PendingMarker ReadForRecovery(string projectRootPath) {
            string canonicalRoot = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(canonicalRoot);
            string markerPath = GetPath(canonicalRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, canonicalRoot);
            return File.Exists(markerPath) ? ReadAndValidate(markerPath, canonicalRoot) : null;
        }

        internal static void ClearUnderLock(string projectRootPath, string transactionId) {
            string canonicalRoot = Path.GetFullPath(projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateTransactionContainer(canonicalRoot);
            string markerPath = GetPath(canonicalRoot);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, canonicalRoot);
            if (!File.Exists(markerPath)) {
                return;
            }
            PendingMarker marker = ReadAndValidate(markerPath, canonicalRoot);
            if (!string.Equals(marker.TransactionId, transactionId, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The authoring transaction pending marker belongs to another transaction.");
            }
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, canonicalRoot);
            File.Delete(markerPath);
        }

        static PendingMarker ReadAndValidate(string markerPath, string projectRootPath) {
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(markerPath, projectRootPath);
            PendingMarker marker;
            try {
                marker = JsonSerializer.Deserialize<PendingMarker>(File.ReadAllText(markerPath), JsonOptions);
            } catch (JsonException exception) {
                throw new InvalidDataException($"The authoring transaction pending marker '{markerPath}' is malformed.", exception);
            }

            if (marker == null || marker.Version != CurrentVersion ||
                !Guid.TryParseExact(marker.TransactionId, "N", out _) ||
                marker.RelativePaths == null || marker.RelativePaths.Count == 0) {
                throw new InvalidDataException($"The authoring transaction pending marker '{markerPath}' is invalid.");
            }

            string assetsRoot = Path.Combine(projectRootPath, "assets");
            HashSet<string> paths = new HashSet<string>(PathComparer);
            for (int index = 0; index < marker.RelativePaths.Count; index++) {
                string normalized = NormalizeAssetPath(assetsRoot, marker.RelativePaths[index]);
                if (!string.Equals(normalized, marker.RelativePaths[index], StringComparison.Ordinal) || !paths.Add(normalized)) {
                    throw new InvalidDataException($"The authoring transaction pending marker '{markerPath}' contains a non-canonical path.");
                }
            }
            return marker;
        }

        static string NormalizeAssetPath(string assetsRoot, string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
                relativePath.IndexOfAny(new[] { '\\', '\t', '\r', '\n' }) >= 0) {
                throw new InvalidDataException("The authoring transaction pending path is not canonical.");
            }
            string canonicalAssetsRoot = Path.GetFullPath(assetsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(canonicalAssetsRoot, relativePath));
            string prefix = canonicalAssetsRoot + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(prefix, comparison)) {
                throw new InvalidDataException("The authoring transaction pending path escapes assets.");
            }
            return Path.GetRelativePath(canonicalAssetsRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        static void WriteAtomically(string projectRootPath, string path, PendingMarker marker) {
            string directory = Path.GetDirectoryName(path);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRootPath);
            Directory.CreateDirectory(directory);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(directory, projectRootPath);
            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, projectRootPath);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporary, projectRootPath);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(marker, JsonOptions);
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)) {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(path, projectRootPath);
                File.Move(temporary, path, true);
            } finally {
                if (File.Exists(temporary)) {
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(temporary, projectRootPath);
                    File.Delete(temporary);
                }
            }
        }

        sealed class OwnerScope : IDisposable {
            readonly string ProjectRootPath;
            bool Disposed;
            public OwnerScope(string projectRootPath) => ProjectRootPath = projectRootPath;
            public void Dispose() {
                if (Disposed) return;
                Disposed = true;
                OwnedProjects?.Remove(ProjectRootPath);
            }
        }

        internal sealed class PendingMarker {
            public int Version { get; set; }
            public string TransactionId { get; set; }
            public List<string> RelativePaths { get; set; }
        }

        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        static readonly StringComparer ProjectComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }
}
