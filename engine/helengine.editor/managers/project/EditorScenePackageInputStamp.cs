namespace helengine.editor {
    /// <summary>
    /// One scene-package key input file as last hashed: its project-relative path, the length and last-write stamp
    /// observed when it was hashed, and the SHA-256 of its content. Persisted by
    /// <see cref="EditorScenePackageInputStampMemo"/>.
    /// </summary>
    internal sealed class EditorScenePackageInputStamp {
        /// <summary>
        /// Project-relative path with forward slashes, exactly as it is appended to the scene-package key.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// File length in bytes when the content hash was computed.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// File last-write time in UTC ticks when the content hash was computed.
        /// </summary>
        public long LastWriteUtcTicks { get; set; }

        /// <summary>
        /// Lowercase hexadecimal SHA-256 of the file content.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;
    }
}
