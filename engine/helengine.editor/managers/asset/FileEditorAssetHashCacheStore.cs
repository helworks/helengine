using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Persists hash-cache documents as atomically replaced JSON files.
    /// </summary>
    sealed class FileEditorAssetHashCacheStore : IEditorAssetHashCacheStore {
        /// <summary>
        /// Maximum number of short attempts made to acquire a cache-path lock.
        /// </summary>
        const int LockAttemptCount = 200;

        /// <summary>
        /// Delay between cache-path lock attempts.
        /// </summary>
        const int LockRetryMilliseconds = 10;

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

            WithCachePathLock(cachePath, () => {
                SaveCore(cachePath, document);
                return true;
            });
        }

        /// <summary>
        /// Merges dirty updates while holding the cache-path lock across read, merge, and replace.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="updates">Dirty path entries from one cache owner.</param>
        /// <returns>The sorted document written by the store.</returns>
        public EditorAssetHashCacheDocument Update(
            string cachePath,
            IReadOnlyDictionary<string, EditorAssetHashCacheEntry> updates,
            IReadOnlyCollection<string> removedPaths) {
            if (updates == null) {
                throw new ArgumentNullException(nameof(updates));
            }
            if (removedPaths == null) {
                throw new ArgumentNullException(nameof(removedPaths));
            }

            return WithCachePathLock(cachePath, () => {
                Dictionary<string, EditorAssetHashCacheEntry> entries = LoadEntriesCore(cachePath);
                foreach (string removedPath in removedPaths) {
                    string relativePath = NormalizeRelativePath(removedPath);
                    if (!string.IsNullOrWhiteSpace(relativePath)) {
                        entries.Remove(relativePath);
                    }
                }
                foreach (KeyValuePair<string, EditorAssetHashCacheEntry> update in updates) {
                    string relativePath = NormalizeRelativePath(update.Key);
                    if (string.IsNullOrWhiteSpace(relativePath) || !IsValidContentHash(update.Value?.ContentHash)) {
                        continue;
                    }

                    entries[relativePath] = new EditorAssetHashCacheEntry {
                        RelativePath = relativePath,
                        Length = update.Value.Length,
                        LastWriteUtcTicks = update.Value.LastWriteUtcTicks,
                        ContentHash = update.Value.ContentHash
                    };
                }

                EditorAssetHashCacheDocument document = new EditorAssetHashCacheDocument {
                    Entries = entries.Values.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToList()
                };
                SaveCore(cachePath, document);
                return document;
            });
        }

        /// <summary>
        /// Loads entries for an update without acquiring the lock a second time.
        /// </summary>
        Dictionary<string, EditorAssetHashCacheEntry> LoadEntriesCore(string cachePath) {
            Dictionary<string, EditorAssetHashCacheEntry> entries = new Dictionary<string, EditorAssetHashCacheEntry>(PathComparer);
            EditorAssetHashCacheDocument storedDocument = Load(cachePath);
            if (storedDocument?.Entries == null) {
                return entries;
            }

            for (int index = 0; index < storedDocument.Entries.Count; index++) {
                EditorAssetHashCacheEntry entry = storedDocument.Entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.RelativePath) || !IsValidContentHash(entry.ContentHash)) {
                    continue;
                }

                string relativePath = NormalizeRelativePath(entry.RelativePath);
                if (!string.IsNullOrWhiteSpace(relativePath)) {
                    entries[relativePath] = entry;
                }
            }

            return entries;
        }

        /// <summary>
        /// Writes one complete document without acquiring a lock.
        /// </summary>
        void SaveCore(string cachePath, EditorAssetHashCacheDocument document) {
            string directoryPath = Path.GetDirectoryName(cachePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new ArgumentException("Cache path must include a writable directory.", nameof(cachePath));
            }

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

        /// <summary>
        /// Executes one cache operation while holding an exclusive cross-process lock file.
        /// </summary>
        T WithCachePathLock<T>(string cachePath, Func<T> operation) {
            if (string.IsNullOrWhiteSpace(cachePath)) {
                throw new ArgumentException("Cache path must be provided.", nameof(cachePath));
            } else if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            string directoryPath = Path.GetDirectoryName(cachePath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new ArgumentException("Cache path must include a writable directory.", nameof(cachePath));
            }

            Directory.CreateDirectory(directoryPath);
            string lockPath = cachePath + ".lock";
            IOException lastFailure = null;
            for (int attempt = 0; attempt < LockAttemptCount; attempt++) {
                FileStream lockHandle = null;
                try {
                    lockHandle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.SequentialScan);
                } catch (IOException exception) {
                    lastFailure = exception;
                    if (attempt + 1 < LockAttemptCount) {
                        Thread.Sleep(LockRetryMilliseconds);
                    }
                    continue;
                }

                using (lockHandle) {
                    return operation();
                }
            }

            throw new IOException($"Unable to acquire the editor asset hash cache lock '{lockPath}'.", lastFailure);
        }

        /// <summary>
        /// Normalizes a persisted relative path.
        /// </summary>
        static string NormalizeRelativePath(string value) {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Gets the operating-system path-key comparer used by cache documents.
        /// </summary>
        static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <summary>
        /// Checks one persisted SHA-256 value.
        /// </summary>
        static bool IsValidContentHash(string value) {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal)) {
                return false;
            }
            for (int index = 7; index < value.Length; index++) {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) {
                    return false;
                }
            }
            return true;
        }
    }
}
