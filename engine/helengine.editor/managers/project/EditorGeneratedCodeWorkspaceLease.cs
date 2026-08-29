using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Holds an exclusive lease for one generated tree across generation and build evaluation.
    /// </summary>
    public sealed class EditorGeneratedCodeWorkspaceLease : IDisposable {
        /// <summary>
        /// Lock directory kept outside authored project trees.
        /// </summary>
        const string LockDirectoryName = "helengine-generated-code-locks";

        /// <summary>
        /// In-process gates keyed by the physical generated-tree identity.
        /// </summary>
        static readonly ConcurrentDictionary<string, object> ProcessGates = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        /// <summary>
        /// Held operating-system file lease.
        /// </summary>
        readonly FileStream LockStream;

        /// <summary>
        /// In-process gate held for the lifetime of this lease.
        /// </summary>
        readonly object ProcessGate;

        /// <summary>
        /// Ensures overlapping cleanup paths release the monitor exactly once.
        /// </summary>
        int Disposed;

        /// <summary>
        /// Initializes one held lease.
        /// </summary>
        /// <param name="physicalWorkspaceRootPath">Resolved physical generated-tree root.</param>
        /// <param name="lockFilePath">Exclusive lock file path.</param>
        /// <param name="lockStream">Held lock stream.</param>
        /// <param name="processGate">Held in-process gate.</param>
        EditorGeneratedCodeWorkspaceLease(
            string physicalWorkspaceRootPath,
            string lockFilePath,
            FileStream lockStream,
            object processGate) {
            WorkspaceRootPath = physicalWorkspaceRootPath;
            LockFilePath = lockFilePath;
            LockStream = lockStream;
            ProcessGate = processGate;
        }

        /// <summary>
        /// Gets the physical generated-tree root protected by this lease.
        /// </summary>
        public string WorkspaceRootPath { get; }

        /// <summary>
        /// Gets the process-wide lock file used by this lease.
        /// </summary>
        public string LockFilePath { get; }

        /// <summary>
        /// Determines whether a path resolves to the same physical generated tree as this lease.
        /// </summary>
        /// <param name="workspaceRootPath">Candidate generated tree path.</param>
        /// <returns><c>true</c> when the candidate maps to this lease's physical tree.</returns>
        internal bool Covers(string workspaceRootPath) {
            string candidateIdentity = NormalizeIdentity(ResolvePhysicalDirectoryPath(workspaceRootPath));
            string rootIdentity = NormalizeIdentity(WorkspaceRootPath);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return candidateIdentity.Equals(rootIdentity, comparison)
                || candidateIdentity.StartsWith(
                    rootIdentity.EndsWith(Path.DirectorySeparatorChar)
                        ? rootIdentity
                        : rootIdentity + Path.DirectorySeparatorChar,
                    comparison);
        }

        /// <summary>
        /// Determines whether a path is exactly the physical root protected by this lease.
        /// </summary>
        /// <param name="workspaceRootPath">Candidate workspace path.</param>
        /// <returns><c>true</c> when the candidate is exactly the leased root.</returns>
        internal bool Matches(string workspaceRootPath) {
            return string.Equals(
                NormalizeIdentity(ResolvePhysicalDirectoryPath(workspaceRootPath)),
                NormalizeIdentity(WorkspaceRootPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// Acquires a physical-path canonicalized exclusive lease.
        /// </summary>
        /// <param name="workspaceRootPath">Generated tree root to protect.</param>
        /// <returns>Held workspace lease.</returns>
        internal static EditorGeneratedCodeWorkspaceLease Acquire(string workspaceRootPath) {
            if (string.IsNullOrWhiteSpace(workspaceRootPath)) {
                throw new ArgumentException("Generated workspace root path must be provided.", nameof(workspaceRootPath));
            }

            string physicalWorkspaceRootPath = ResolvePhysicalDirectoryPath(workspaceRootPath);
            string identity = NormalizeIdentity(physicalWorkspaceRootPath);
            object processGate = ProcessGates.GetOrAdd(identity, static _ => new object());
            Monitor.Enter(processGate);

            try {
                string lockFilePath = ResolveLockFilePath(identity);
                FileStream lockStream = AcquireExclusiveFile(lockFilePath);
                return new EditorGeneratedCodeWorkspaceLease(physicalWorkspaceRootPath, lockFilePath, lockStream, processGate);
            } catch {
                Monitor.Exit(processGate);
                throw;
            }
        }

        /// <summary>
        /// Releases the operating-system lock and then the in-process gate.
        /// </summary>
        public void Dispose() {
            if (Interlocked.Exchange(ref Disposed, 1) != 0) {
                return;
            }

            try {
                LockStream.Dispose();
            } finally {
                Monitor.Exit(ProcessGate);
            }
        }

        /// <summary>
        /// Opens the lock file exclusively, retrying only while another process holds it.
        /// </summary>
        /// <param name="lockFilePath">Lock file to open.</param>
        /// <returns>Exclusive lock stream.</returns>
        static FileStream AcquireExclusiveFile(string lockFilePath) {
            string lockDirectoryPath = Path.GetDirectoryName(lockFilePath);
            if (!string.IsNullOrWhiteSpace(lockDirectoryPath)) {
                Directory.CreateDirectory(lockDirectoryPath);
            }

            while (true) {
                try {
                    return new FileStream(
                        lockFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.None);
                } catch (IOException) {
                    Thread.Yield();
                }
            }
        }

        /// <summary>
        /// Computes the stable lock path from a physical workspace identity.
        /// </summary>
        /// <param name="identity">Normalized physical path identity.</param>
        /// <returns>Lock path outside authored trees.</returns>
        static string ResolveLockFilePath(string identity) {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
            return Path.Combine(Path.GetTempPath(), LockDirectoryName, hash + ".lock");
        }

        /// <summary>
        /// Resolves an existing-link-aware physical path while preserving not-yet-created descendants.
        /// </summary>
        /// <param name="path">Path to resolve.</param>
        /// <returns>Physical path identity path.</returns>
        static string ResolvePhysicalDirectoryPath(string path) {
            string fullPath = Path.GetFullPath(path);
            string rootPath = Path.GetPathRoot(fullPath) ?? string.Empty;
            string currentPath = rootPath;
            string relativePath = rootPath.Length == 0 ? fullPath : fullPath[rootPath.Length..];
            string[] segments = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments) {
                string candidatePath = Path.Combine(currentPath, segment);
                DirectoryInfo candidateDirectory = new DirectoryInfo(candidatePath);
                if (candidateDirectory.Exists) {
                    try {
                        FileSystemInfo resolvedTarget = candidateDirectory.ResolveLinkTarget(true);
                        if (resolvedTarget is DirectoryInfo resolvedDirectory) {
                            currentPath = resolvedDirectory.FullName;
                            continue;
                        }
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    }
                }

                currentPath = candidateDirectory.FullName;
            }

            return TrimDirectorySeparators(currentPath);
        }

        /// <summary>
        /// Normalizes a physical path for the host filesystem's identity semantics.
        /// </summary>
        /// <param name="physicalPath">Resolved physical path.</param>
        /// <returns>Stable lock identity.</returns>
        static string NormalizeIdentity(string physicalPath) {
            return OperatingSystem.IsWindows()
                ? physicalPath.ToUpperInvariant()
                : physicalPath;
        }

        /// <summary>
        /// Removes non-root trailing directory separators.
        /// </summary>
        /// <param name="path">Path to trim.</param>
        /// <returns>Trimmed path.</returns>
        static string TrimDirectorySeparators(string path) {
            string rootPath = Path.GetPathRoot(path) ?? string.Empty;
            return path.Length > rootPath.Length
                ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
        }
    }
}
