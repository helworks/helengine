namespace helengine.editor {
    /// <summary>
    /// Describes the type of file system change detected by a folder watcher.
    /// </summary>
    public enum FolderWatchEventKind {
        /// <summary>
        /// Indicates a file or directory was created.
        /// </summary>
        Created,
        /// <summary>
        /// Indicates a file or directory was changed.
        /// </summary>
        Changed,
        /// <summary>
        /// Indicates a file or directory was deleted.
        /// </summary>
        Deleted,
        /// <summary>
        /// Indicates a file or directory was renamed.
        /// </summary>
        Renamed
    }
}
