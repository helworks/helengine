namespace helengine.editor {
    /// <summary>
    /// Temporary queued-build executor used until real platform build execution is implemented.
    /// </summary>
    public sealed class EditorPlaceholderBuildExecutor : IEditorBuildExecutor {
        /// <summary>
        /// Fixed failure message returned by the placeholder executor.
        /// </summary>
        const string NotImplementedMessage = "Build execution is not implemented yet.";

        /// <summary>
        /// Executes one queued build item by returning the placeholder failure result.
        /// </summary>
        /// <param name="queueItem">Queued build item that would be executed by a future real builder.</param>
        /// <returns>Failure result indicating build execution is not implemented yet.</returns>
        public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            return EditorBuildExecutionResult.Failure(NotImplementedMessage);
        }
    }
}
