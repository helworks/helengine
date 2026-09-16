using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// One file's state as observed by the last full or incremental identity reconcile.
    /// </summary>
    internal sealed class EditorAssetIdentitySnapshotFile {
        /// <summary>
        /// Assets-relative path with forward slashes.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// File length when observed.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// File last-write time in UTC ticks when observed.
        /// </summary>
        public long LastWriteUtcTicks { get; set; }

        /// <summary>
        /// Whether the classifier considered the file an authored asset.
        /// </summary>
        public bool Authored { get; set; }

        /// <summary>
        /// Browser entry kind assigned by the classifier; only meaningful when authored.
        /// </summary>
        public string EntryKind { get; set; } = string.Empty;

        /// <summary>
        /// Whether identity is embedded in the asset payload rather than in a sidecar.
        /// </summary>
        public bool EmbeddedIdentity { get; set; }

        /// <summary>
        /// Sidecar length when identity is external; ignored otherwise.
        /// </summary>
        public long SidecarLength { get; set; }

        /// <summary>
        /// Sidecar last-write time in UTC ticks when identity is external; ignored otherwise.
        /// </summary>
        public long SidecarLastWriteUtcTicks { get; set; }

        /// <summary>
        /// Current asset id; only meaningful when authored.
        /// </summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>
        /// Former asset ids recorded in the identity document; only meaningful when authored.
        /// </summary>
        public List<string> FormerAssetIds { get; set; } = new List<string>();
    }

    /// <summary>
    /// Persisted result of an identity reconcile, so the next boot only opens files whose stamps changed.
    /// </summary>
    internal sealed class EditorAssetIdentitySnapshotDocument {
        /// <summary>
        /// Format version; a mismatch forces a full reconcile.
        /// </summary>
        public int Version { get; set; } = EditorAssetIdentitySnapshotStore.CurrentVersion;

        /// <summary>
        /// Every non-sidecar file under the assets root at reconcile time.
        /// </summary>
        public List<EditorAssetIdentitySnapshotFile> Files { get; set; } = new List<EditorAssetIdentitySnapshotFile>();
    }

    /// <summary>
    /// Reads and writes the identity snapshot under the project's editor cache. A missing, unreadable, or
    /// mismatched snapshot simply means the next reconcile is a full one.
    /// </summary>
    internal sealed class EditorAssetIdentitySnapshotStore {
        /// <summary>
        /// Current snapshot format version. Bump when the classifier or identity rules change.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Snapshot file name inside cache/editor.
        /// </summary>
        public const string FileName = "asset-identity-snapshot.json";

        /// <summary>
        /// JSON options shared by load and save.
        /// </summary>
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Absolute project root that owns the snapshot.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates a store for one project.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root.</param>
        public EditorAssetIdentitySnapshotStore(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        /// <summary>
        /// Gets the absolute snapshot path.
        /// </summary>
        public string SnapshotPath => Path.Combine(ProjectRootPath, "cache", "editor", FileName);

        /// <summary>
        /// Loads the snapshot when present and current.
        /// </summary>
        /// <returns>The snapshot, or null when absent, unreadable, or of another version.</returns>
        public EditorAssetIdentitySnapshotDocument Load() {
            string snapshotPath = SnapshotPath;
            if (!File.Exists(snapshotPath)) {
                return null;
            }

            try {
                string json = Encoding.UTF8.GetString(EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, snapshotPath));
                EditorAssetIdentitySnapshotDocument document = JsonSerializer.Deserialize<EditorAssetIdentitySnapshotDocument>(json, JsonOptions);
                if (document == null || document.Version != CurrentVersion || document.Files == null) {
                    return null;
                }

                return document;
            } catch (Exception exception) when (exception is IOException || exception is JsonException || exception is UnauthorizedAccessException || exception is InvalidDataException) {
                return null;
            }
        }

        /// <summary>
        /// Atomically writes the snapshot.
        /// </summary>
        /// <param name="document">Snapshot to persist.</param>
        public void Save(EditorAssetIdentitySnapshotDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            document.Version = CurrentVersion;
            document.Files.Sort((left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(document, JsonOptions));
            string snapshotPath = SnapshotPath;
            EditorAuthoringMutationScope.EnsureDirectory(ProjectRootPath, Path.GetDirectoryName(snapshotPath));
            EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, snapshotPath, bytes);
        }

        /// <summary>
        /// Deletes the snapshot when present, forcing the next reconcile to be full.
        /// </summary>
        public void Delete() {
            string snapshotPath = SnapshotPath;
            if (File.Exists(snapshotPath)) {
                EditorAuthoringMutationScope.DeleteLeaf(ProjectRootPath, snapshotPath);
            }
        }
    }
}
