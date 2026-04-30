namespace helengine.editor {
    /// <summary>
    /// Describes the persisted execution state of one queued local build item.
    /// </summary>
    public enum EditorBuildQueueItemStatus {
        /// <summary>
        /// Indicates the queued build item has not started execution yet.
        /// </summary>
        Pending,

        /// <summary>
        /// Indicates the queued build item is currently being executed.
        /// </summary>
        Running,

        /// <summary>
        /// Indicates the queued build item completed successfully.
        /// </summary>
        Done,

        /// <summary>
        /// Indicates the queued build item failed during execution or validation.
        /// </summary>
        Failed
    }
}
