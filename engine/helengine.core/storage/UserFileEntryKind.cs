namespace helengine {
    /// <summary>Filesystem object type reported without following symbolic links.</summary>
    public enum UserFileEntryKind {
        /// <summary>A regular file that may be opened for a bounded read.</summary>
        File,
        /// <summary>A directory that may be listed separately.</summary>
        Directory,
        /// <summary>A symbolic link that cannot be traversed or read.</summary>
        SymbolicLink,
        /// <summary>A special object that cannot be opened by this service.</summary>
        Other
    }
}
