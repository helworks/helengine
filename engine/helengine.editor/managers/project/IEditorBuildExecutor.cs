namespace helengine.editor {
    /// <summary>
    /// Executes one persisted queued build item for the local editor build queue workflow.
    /// </summary>
    public interface IEditorBuildExecutor {
        /// <summary>
        /// Executes the supplied queued build item and returns one structured result describing the outcome.
        /// </summary>
        /// <param name="queueItem">Queued build item that should be executed.</param>
        /// <returns>Structured execution result describing success or failure.</returns>
        EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem);
    }
}
