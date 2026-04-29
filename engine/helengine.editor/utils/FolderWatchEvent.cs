namespace helengine.editor {
    /// <summary>
    /// Represents a single file system change captured by a folder watcher.
    /// </summary>
    public class FolderWatchEvent {
        /// <summary>
        /// Initializes a change event for create, change, or delete operations.
        /// </summary>
        /// <param name="kind">Change kind for this event.</param>
        /// <param name="fullPath">Absolute path to the affected item.</param>
        public FolderWatchEvent(FolderWatchEventKind kind, string fullPath) {
            if (kind == FolderWatchEventKind.Renamed) {
                throw new ArgumentException("Rename events require an old path.", nameof(kind));
            }

            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided.", nameof(fullPath));
            }

            Kind = kind;
            FullPath = fullPath;
            OldFullPath = string.Empty;
            HasOldFullPath = false;
        }

        /// <summary>
        /// Initializes a change event for a rename operation.
        /// </summary>
        /// <param name="fullPath">New absolute path of the item.</param>
        /// <param name="oldFullPath">Previous absolute path of the item.</param>
        public FolderWatchEvent(string fullPath, string oldFullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided.", nameof(fullPath));
            }

            if (string.IsNullOrWhiteSpace(oldFullPath)) {
                throw new ArgumentException("Old full path must be provided.", nameof(oldFullPath));
            }

            Kind = FolderWatchEventKind.Renamed;
            FullPath = fullPath;
            OldFullPath = oldFullPath;
            HasOldFullPath = true;
        }

        /// <summary>
        /// Gets the change kind for this event.
        /// </summary>
        public FolderWatchEventKind Kind { get; }

        /// <summary>
        /// Gets the absolute path of the affected item.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the previous absolute path when a rename occurred.
        /// </summary>
        public string OldFullPath { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="OldFullPath"/> is available.
        /// </summary>
        public bool HasOldFullPath { get; }
    }
}
