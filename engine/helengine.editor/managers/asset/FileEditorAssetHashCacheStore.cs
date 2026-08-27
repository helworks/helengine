using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Persists hash-cache documents as atomically replaced JSON files.
    /// </summary>
    sealed class FileEditorAssetHashCacheStore : IEditorAssetHashCacheStore {
        /// <summary>
        /// JSON options for the current hash-cache document.
        /// </summary>
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Loads a cache document when the path contains valid current data.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <returns>Loaded document, or null when it is absent or invalid.</returns>
        public EditorAssetHashCacheDocument Load(string cachePath) {
            if (!File.Exists(cachePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(cachePath);
                EditorAssetHashCacheDocument document = JsonSerializer.Deserialize<EditorAssetHashCacheDocument>(json, JsonOptions);
                if (document == null || document.Entries == null) {
                    return null;
                }

                return document;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Atomically stores one complete cache document.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="document">Sorted cache document.</param>
        public void Save(string cachePath, EditorAssetHashCacheDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            string directoryPath = Path.GetDirectoryName(cachePath);
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                string json = JsonSerializer.Serialize(document, JsonOptions);
                File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
                File.Move(temporaryPath, cachePath, true);
            } finally {
                if (File.Exists(temporaryPath)) {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
