namespace helengine.editor {
    /// <summary>
    /// Persisted form of <see cref="EditorScenePackageInputStampMemo"/>: every key input file whose stamp may be
    /// trusted on the next scene-package key computation.
    /// </summary>
    internal sealed class EditorScenePackageInputStampDocument {
        /// <summary>
        /// Format version; a mismatch makes the next key computation hash every input.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Remembered input files in ordinal path order.
        /// </summary>
        public List<EditorScenePackageInputStamp> Files { get; set; } = new List<EditorScenePackageInputStamp>();
    }
}
