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
        /// Creates a standalone hasher for callers that provide a non-project file.
        /// </summary>
        public AssetFileHasher() {
        }

        /// <summary>
        /// Computes a SHA-256 hash for the specified file.
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
            string projectRootPath = ResolveProjectRootForAuthoredPath(fullPath);
            if (!string.IsNullOrWhiteSpace(projectRootPath)) {
                using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
                    projectRootPath,
                    Path.GetDirectoryName(fullPath));
                using EditorAuthoringVerifiedFile verifiedFile = scope.OpenVerifiedFile(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                return ComputeHash(verifiedFile.Stream);
            }

            using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ComputeHash(stream);
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

        string ResolveProjectRootForAuthoredPath(string fullPath) {
            if (!string.IsNullOrWhiteSpace(ProjectRootPath)) {
                return ProjectRootPath;
            }
            return EditorProjectPaths.ResolveStandaloneRoot(fullPath);
        }
    }
}
