namespace helengine.editor {
    /// <summary>
    /// Reads file attributes for paths that may not exist without raising and swallowing exceptions.
    /// Boot-time reconciliation walks thousands of ancestors and candidate paths, so every
    /// missing-path probe must be exception-free to keep the debugger and the boot quiet.
    /// </summary>
    static class EditorFileAttributesProbe {
        /// <summary>
        /// Reads the attributes of a file or directory when it exists.
        /// </summary>
        /// <param name="path">Absolute file or directory path.</param>
        /// <param name="attributes">Attributes when the path exists; otherwise undefined.</param>
        /// <returns>True when the path exists and its attributes were read.</returns>
        public static bool TryGetAttributes(string path, out FileAttributes attributes) {
            // FileInfo.Attributes also works for directories and reports an invalid marker
            // for a missing path instead of throwing FileNotFound or DirectoryNotFound.
            attributes = new FileInfo(path).Attributes;
            return (int)attributes != -1;
        }

        /// <summary>
        /// Determines whether an existing path is a reparse point; a missing path is not.
        /// </summary>
        /// <param name="path">Absolute file or directory path.</param>
        /// <returns>True when the path exists and carries the reparse-point attribute.</returns>
        public static bool IsReparsePoint(string path) {
            return TryGetAttributes(path, out FileAttributes attributes)
                && (attributes & FileAttributes.ReparsePoint) != 0;
        }
    }
}
