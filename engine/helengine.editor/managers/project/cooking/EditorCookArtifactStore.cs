using System.Security.Cryptography;

namespace helengine.editor {

    /// <summary>
    /// Publishes and reads immutable cooked payloads under a workspace-owned artifact root.
    /// </summary>
    public sealed class EditorCookArtifactStore {
        readonly string RootPath;

        /// <summary>
        /// Initializes an artifact store.
        /// </summary>
        /// <param name="rootPath">Workspace-owned store root.</param>
        public EditorCookArtifactStore(string rootPath) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Artifact store root must be provided.", nameof(rootPath));
            }

            RootPath = Path.GetFullPath(rootPath);
            Directory.CreateDirectory(RootPath);
        }

        /// <summary>
        /// Publishes bytes once using a staging file and atomic move.
        /// </summary>
        /// <param name="cookKey">Cook key used as object identity.</param>
        /// <param name="payload">Payload bytes.</param>
        /// <returns>Validated receipt.</returns>
        public EditorCookArtifactReceipt Publish(EditorAssetCookNodeKey cookKey, byte[] payload) {
            if (cookKey == null) {
                throw new ArgumentNullException(nameof(cookKey));
            }
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }

            string objectName = SanitizeKey(cookKey.Value);
            string relativePath = Path.Combine("objects", objectName, "payload.bin");
            string fullPath = Path.Combine(RootPath, relativePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directoryPath);
            if (!File.Exists(fullPath)) {
                string stagingPath = fullPath + ".staging-" + Guid.NewGuid().ToString("N");
                File.WriteAllBytes(stagingPath, payload);
                File.Move(stagingPath, fullPath, false);
            }

            return CreateReceipt(relativePath, fullPath);
        }

        /// <summary>
        /// Reads an existing valid object from the store.
        /// </summary>
        /// <param name="cookKey">Cook key.</param>
        /// <param name="payload">Existing payload.</param>
        /// <param name="receipt">Existing receipt.</param>
        /// <returns>True when the object exists and can be read.</returns>
        public bool TryRead(EditorAssetCookNodeKey cookKey, out byte[] payload, out EditorCookArtifactReceipt receipt) {
            if (cookKey == null) {
                throw new ArgumentNullException(nameof(cookKey));
            }

            string relativePath = Path.Combine("objects", SanitizeKey(cookKey.Value), "payload.bin");
            string fullPath = Path.Combine(RootPath, relativePath);
            if (!File.Exists(fullPath)) {
                payload = null;
                receipt = null;
                return false;
            }

            payload = File.ReadAllBytes(fullPath);
            receipt = CreateReceipt(relativePath, fullPath);
            return true;
        }

        static EditorCookArtifactReceipt CreateReceipt(string relativePath, string fullPath) {
            using FileStream stream = File.OpenRead(fullPath);
            using SHA256 sha256 = SHA256.Create();
            return new EditorCookArtifactReceipt(
                "sha256:" + Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant(),
                stream.Length,
                relativePath.Replace(Path.DirectorySeparatorChar, '/'));
        }

        static string SanitizeKey(string key) {
            return key.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
