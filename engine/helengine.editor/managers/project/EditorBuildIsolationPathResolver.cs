using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Resolves stable per-project build-isolation roots so concurrent platform builds do not share mutable output trees.
    /// </summary>
    internal sealed class EditorBuildIsolationPathResolver {
        /// <summary>
        /// Top-level temporary folder used to hold isolated build state.
        /// </summary>
        const string IsolationFolderName = "helengine-builds";

        /// <summary>
        /// Number of SHA-256 bytes retained in the stable project hash segment.
        /// </summary>
        const int ProjectHashByteCount = 16;

        /// <summary>
        /// Absolute authored project root path used to seed stable isolation roots.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Stable hash segment derived from the authored project root path.
        /// </summary>
        readonly string ProjectHashSegment;

        /// <summary>
        /// Initializes one resolver for the supplied authored project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative authored project root path.</param>
        public EditorBuildIsolationPathResolver(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectHashSegment = ComputeProjectHashSegment(ProjectRootPath);
        }

        /// <summary>
        /// Resolves the stable isolated root for one target platform.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute isolated root path for the supplied platform.</returns>
        public string ResolvePlatformRootPath(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return Path.Combine(
                Path.GetTempPath(),
                IsolationFolderName,
                ProjectHashSegment,
                SanitizePathSegment(platformId));
        }

        /// <summary>
        /// Resolves the execution root used by one queued workspace run for the supplied platform.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <param name="queueItemId">Stable queued build item identifier.</param>
        /// <returns>Absolute isolated execution root path for the queued workspace run.</returns>
        public string ResolveWorkspaceExecutionRootPath(string platformId, string queueItemId) {
            if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id must be provided.", nameof(queueItemId));
            }

            return Path.Combine(
                ResolvePlatformRootPath(platformId),
                "workspace",
                SanitizePathSegment(queueItemId));
        }

        /// <summary>
        /// Resolves the generated managed-code output root used by headless platform builds for the supplied platform.
        /// </summary>
        /// <param name="platformId">Stable target platform identifier.</param>
        /// <returns>Absolute isolated generated managed-code output root path.</returns>
        public string ResolveGeneratedCodeOutputRootPath(string platformId) {
            return Path.Combine(ResolvePlatformRootPath(platformId), "generated-dotnet");
        }

        /// <summary>
        /// Computes one stable project hash segment from the canonical authored project root path.
        /// </summary>
        /// <param name="projectRootPath">Absolute authored project root path.</param>
        /// <returns>Filesystem-safe lowercase hexadecimal hash segment.</returns>
        static string ComputeProjectHashSegment(string projectRootPath) {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(projectRootPath));
            StringBuilder builder = new StringBuilder(ProjectHashByteCount * 2);
            for (int index = 0; index < ProjectHashByteCount; index++) {
                builder.Append(hashBytes[index].ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Replaces filesystem-invalid characters in one path segment with underscores.
        /// </summary>
        /// <param name="value">Untrusted segment value.</param>
        /// <returns>Filesystem-safe segment value.</returns>
        static string SanitizePathSegment(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Path segment must be provided.", nameof(value));
            }

            StringBuilder builder = new StringBuilder(value.Length);
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int index = 0; index < value.Length; index++) {
                char currentCharacter = value[index];
                builder.Append(Array.IndexOf(invalidCharacters, currentCharacter) >= 0 ? '_' : currentCharacter);
            }

            return builder.ToString();
        }
    }
}
