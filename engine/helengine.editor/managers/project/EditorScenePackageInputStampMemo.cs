using System.Text;
using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Remembers the content hash of each scene-package key input file by its length and last-write stamp, so a key
    /// computation re-hashes only files that are new or whose stamp changed, and forgets files that disappeared.
    /// <para>
    /// Trust model: a file whose length and last-write time both match the remembered stamp is assumed to hold the
    /// remembered content. An edit that keeps both the same is not noticed. This is the same trust the asset identity
    /// snapshot (<see cref="EditorAssetIdentitySnapshotStore"/>) and <see cref="EditorAssetHashCache"/> already place
    /// in stamps. Hashes, not stamps, remain the key's identity: a changed stamp always leads to a re-hash, and a
    /// re-hash that yields the same content yields the same key.
    /// </para>
    /// <para>
    /// A file modified within <see cref="RecentWriteWindow"/> of the pass start is hashed but not remembered: its
    /// directory entry may not yet reflect a write still in progress, or a second write may land inside the stamp's
    /// resolution, so it is re-hashed on the next pass instead of being trusted.
    /// </para>
    /// The memo lives in the project's disposable cache; a missing, unreadable, or other-version memo just means every
    /// input is hashed again. The key it feeds is a cache identity, not an integrity boundary: anyone who can write the
    /// project cache can already make a stale entry hit, with or without the memo.
    /// </summary>
    internal sealed class EditorScenePackageInputStampMemo {
        /// <summary>
        /// Current memo format version.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Memo file name inside the scene-package cache folder.
        /// </summary>
        public const string FileName = "input-stamps.json";

        /// <summary>
        /// Files written this recently before the pass started are hashed but not remembered.
        /// </summary>
        static readonly TimeSpan RecentWriteWindow = TimeSpan.FromSeconds(2);

        /// <summary>
        /// JSON options shared by load and save.
        /// </summary>
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Absolute memo file path.
        /// </summary>
        readonly string MemoPath;

        /// <summary>
        /// Stamps loaded from the memo file, keyed by project-relative path.
        /// </summary>
        readonly Dictionary<string, EditorScenePackageInputStamp> RememberedStamps;

        /// <summary>
        /// Stamps observed during this pass that may be trusted next time, keyed by project-relative path.
        /// </summary>
        readonly Dictionary<string, EditorScenePackageInputStamp> ObservedStamps = new Dictionary<string, EditorScenePackageInputStamp>(StringComparer.Ordinal);

        /// <summary>
        /// Files whose last-write time is at or after this UTC tick count are not remembered.
        /// </summary>
        readonly long RememberBeforeUtcTicks;

        /// <summary>
        /// Creates a memo pass over previously remembered stamps.
        /// </summary>
        /// <param name="memoPath">Absolute memo file path.</param>
        /// <param name="rememberedStamps">Stamps loaded from the memo file.</param>
        /// <param name="passStartUtc">UTC time captured before the pass enumerates any input.</param>
        EditorScenePackageInputStampMemo(string memoPath, Dictionary<string, EditorScenePackageInputStamp> rememberedStamps, DateTime passStartUtc) {
            MemoPath = memoPath;
            RememberedStamps = rememberedStamps;
            RememberBeforeUtcTicks = passStartUtc.Ticks - RecentWriteWindow.Ticks;
        }

        /// <summary>
        /// Number of inputs whose content was hashed during this pass.
        /// </summary>
        public int HashedFileCount { get; private set; }

        /// <summary>
        /// Number of inputs whose remembered hash was reused during this pass.
        /// </summary>
        public int ReusedFileCount { get; private set; }

        /// <summary>
        /// Starts a memo pass, loading the stamps the previous pass remembered.
        /// </summary>
        /// <param name="memoPath">Absolute memo file path.</param>
        /// <returns>Memo pass ready to resolve input hashes.</returns>
        public static EditorScenePackageInputStampMemo Begin(string memoPath) {
            if (string.IsNullOrWhiteSpace(memoPath)) {
                throw new ArgumentException("Memo path must be provided.", nameof(memoPath));
            }

            DateTime passStartUtc = DateTime.UtcNow;
            return new EditorScenePackageInputStampMemo(Path.GetFullPath(memoPath), Load(memoPath), passStartUtc);
        }

        /// <summary>
        /// Returns the content hash of one input file, reusing the remembered hash when the file's length and
        /// last-write stamp both match, and hashing the file otherwise.
        /// </summary>
        /// <param name="relativePath">Project-relative path the key appends for the file.</param>
        /// <param name="file">File entry from the directory listing; its length and last-write time are the stamp.</param>
        /// <param name="fileHasher">Verified hasher used when the stamp does not match.</param>
        /// <returns>Lowercase hexadecimal SHA-256 of the file content.</returns>
        public string ResolveHash(string relativePath, FileInfo file, AssetFileHasher fileHasher) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (file == null) {
                throw new ArgumentNullException(nameof(file));
            } else if (fileHasher == null) {
                throw new ArgumentNullException(nameof(fileHasher));
            }

            long length = file.Length;
            long lastWriteUtcTicks = file.LastWriteTimeUtc.Ticks;
            string contentHash;
            if (RememberedStamps.TryGetValue(relativePath, out EditorScenePackageInputStamp remembered)
                && remembered.Length == length
                && remembered.LastWriteUtcTicks == lastWriteUtcTicks) {
                contentHash = remembered.ContentHash;
                ReusedFileCount++;
            } else {
                contentHash = fileHasher.ComputeHash(file.FullName);
                HashedFileCount++;
            }

            if (lastWriteUtcTicks < RememberBeforeUtcTicks) {
                ObservedStamps[relativePath] = new EditorScenePackageInputStamp {
                    RelativePath = relativePath,
                    Length = length,
                    LastWriteUtcTicks = lastWriteUtcTicks,
                    ContentHash = contentHash
                };
            }

            return contentHash;
        }

        /// <summary>
        /// Replaces the memo file with the stamps observed during this pass, which drops files that no longer exist.
        /// The file is left untouched when nothing changed. A memo that cannot be written only costs re-hashing on the
        /// next pass, so a write failure is logged as a warning instead of failing the cook.
        /// </summary>
        public void Save() {
            if (!HasChanges()) {
                return;
            }

            EditorScenePackageInputStampDocument document = new EditorScenePackageInputStampDocument {
                Version = CurrentVersion,
                Files = ObservedStamps.Values.OrderBy(stamp => stamp.RelativePath, StringComparer.Ordinal).ToList()
            };
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(document, JsonOptions));
            string directoryPath = Path.GetDirectoryName(MemoPath)
                ?? throw new InvalidOperationException("The scene-package input memo has no parent directory.");
            string stagingPath = Path.Combine(directoryPath, FileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                Directory.CreateDirectory(directoryPath);
                File.WriteAllBytes(stagingPath, bytes);
                File.Move(stagingPath, MemoPath, true);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) {
                Logger.WriteWarning($"Scene-package input memo was not written: {exception.Message}");
                DeleteStagingFile(stagingPath);
            }
        }

        /// <summary>
        /// Removes a staging file left by a failed save. The memo is disposable cache state, so a staging file that
        /// cannot be removed is only logged; it must not turn the logged save failure into a failed cook.
        /// </summary>
        /// <param name="stagingPath">Staging file to remove.</param>
        static void DeleteStagingFile(string stagingPath) {
            if (!File.Exists(stagingPath)) {
                return;
            }

            try {
                File.Delete(stagingPath);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) {
                Logger.WriteWarning($"Scene-package input memo staging file '{stagingPath}' was not removed: {exception.Message}");
            }
        }

        /// <summary>
        /// Reports whether the stamps observed during this pass differ from the ones loaded at its start.
        /// </summary>
        /// <returns>True when the memo file should be rewritten.</returns>
        bool HasChanges() {
            if (ObservedStamps.Count != RememberedStamps.Count) {
                return true;
            }

            foreach (EditorScenePackageInputStamp observed in ObservedStamps.Values) {
                if (!RememberedStamps.TryGetValue(observed.RelativePath, out EditorScenePackageInputStamp remembered)
                    || remembered.Length != observed.Length
                    || remembered.LastWriteUtcTicks != observed.LastWriteUtcTicks
                    || !string.Equals(remembered.ContentHash, observed.ContentHash, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Loads the remembered stamps, skipping malformed entries.
        /// </summary>
        /// <param name="memoPath">Absolute memo file path.</param>
        /// <returns>Remembered stamps keyed by project-relative path; empty when the memo is absent, unreadable, or of another version.</returns>
        static Dictionary<string, EditorScenePackageInputStamp> Load(string memoPath) {
            Dictionary<string, EditorScenePackageInputStamp> stamps = new Dictionary<string, EditorScenePackageInputStamp>(StringComparer.Ordinal);
            if (!File.Exists(memoPath)) {
                return stamps;
            }

            EditorScenePackageInputStampDocument document;
            try {
                document = JsonSerializer.Deserialize<EditorScenePackageInputStampDocument>(File.ReadAllBytes(memoPath), JsonOptions);
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException) {
                return stamps;
            }

            if (document == null || document.Version != CurrentVersion || document.Files == null) {
                return stamps;
            }

            foreach (EditorScenePackageInputStamp stamp in document.Files) {
                if (IsWellFormed(stamp)) {
                    stamps[stamp.RelativePath] = stamp;
                }
            }

            return stamps;
        }

        /// <summary>
        /// Rejects entries that could not have been written by <see cref="Save"/>.
        /// </summary>
        /// <param name="stamp">Loaded entry.</param>
        /// <returns>True when the entry has a path, a non-negative length, and a lowercase SHA-256 hash.</returns>
        static bool IsWellFormed(EditorScenePackageInputStamp stamp) {
            if (stamp == null || string.IsNullOrWhiteSpace(stamp.RelativePath) || stamp.Length < 0 || stamp.ContentHash == null || stamp.ContentHash.Length != 64) {
                return false;
            }

            foreach (char character in stamp.ContentHash) {
                if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f')) {
                    return false;
                }
            }

            return true;
        }
    }
}
