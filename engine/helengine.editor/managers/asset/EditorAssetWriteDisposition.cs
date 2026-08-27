namespace helengine.editor {
    /// <summary>
    /// Describes the destination state reported by a native asset write.
    /// </summary>
    public enum EditorAssetWriteDisposition {
        /// <summary>
        /// The destination did not exist before authoring.
        /// </summary>
        Created,

        /// <summary>
        /// The destination existed and was written.
        /// </summary>
        Changed,

        /// <summary>
        /// The destination already contained the authored output.
        /// </summary>
        Unchanged
    }
}
