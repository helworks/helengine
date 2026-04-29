namespace helengine.editor {
    /// <summary>
    /// Computes stable hashes for asset files to track source content.
    /// </summary>
    public class AssetFileHasher {
        /// <summary>
        /// Computes a SHA-256 hash for the specified file.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the file.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public string ComputeHash(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("Asset file was not found.", filePath);
            }

            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ComputeHash(stream);
        }

        /// <summary>
        /// Computes a SHA-256 hash for the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing the file contents.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public string ComputeHash(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using var hasher = System.Security.Cryptography.SHA256.Create();
            byte[] hash = hasher.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
