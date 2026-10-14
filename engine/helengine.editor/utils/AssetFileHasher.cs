namespace helengine.editor {
    /// <summary>
    /// Computes stable hashes for asset files to track source content.
    /// </summary>
    public class AssetFileHasher {
        /// <summary>
        /// Authoritative project root used for secure authored-file reads.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates a hasher scoped to one project root.
        /// </summary>
        /// <param name="projectRootPath">Canonical project root.</param>
        public AssetFileHasher(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Computes a SHA-256 hash for the specified file. The file is read through a verified, pinned directory
        /// chain. When an <see cref="EditorAuthoringReadBatch"/> for the same project is active on this thread, the
        /// batch's scope for the file's directory is reused, so a bulk hash verifies each directory chain once
        /// instead of once per file; the leaf is still checked and verified on every open.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the file.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public virtual string ComputeHash(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("Asset file was not found.", filePath);
            }

            string fullPath = Path.GetFullPath(filePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            EditorAuthoringMutationScope batchScope = EditorAuthoringReadBatch.TryGetScope(ProjectRootPath, directoryPath);
            if (batchScope != null) {
                return ComputeHash(batchScope, fullPath);
            }

            using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(ProjectRootPath, directoryPath);
            return ComputeHash(scope, fullPath);
        }

        /// <summary>
        /// Computes a SHA-256 hash for the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing the file contents.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public virtual string ComputeHash(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using var hasher = System.Security.Cryptography.SHA256.Create();
            byte[] hash = hasher.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Hashes one regular-file leaf opened through a scope whose directory chain is already verified and pinned.
        /// </summary>
        /// <param name="scope">Verified scope pinning the file's directory.</param>
        /// <param name="fullPath">Absolute path of the file directly beneath the scope's directory.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        string ComputeHash(EditorAuthoringMutationScope scope, string fullPath) {
            using EditorAuthoringVerifiedFile verifiedFile = scope.OpenVerifiedFile(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            return ComputeHash(verifiedFile.Stream);
        }

    }
}
