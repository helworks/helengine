namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides one deterministic build executor for queue-service and session tests.
    /// </summary>
    internal sealed class TestEditorBuildExecutor : IEditorBuildExecutor {
        /// <summary>
        /// Gets the queued build results returned to the caller in execution order.
        /// </summary>
        Queue<EditorBuildExecutionResult> Results { get; }

        /// <summary>
        /// Gets the queue-item identifiers executed so far.
        /// </summary>
        public List<string> ExecutedQueueItemIds { get; } = [];

        /// <summary>
        /// Gets the queue-item statuses observed at execution time.
        /// </summary>
        public List<EditorBuildQueueItemStatus> ObservedStatuses { get; } = [];

        /// <summary>
        /// Initializes one deterministic test build executor.
        /// </summary>
        /// <param name="results">Ordered execution results returned to the caller.</param>
        public TestEditorBuildExecutor(IEnumerable<EditorBuildExecutionResult> results) {
            if (results == null) {
                throw new ArgumentNullException(nameof(results));
            }

            Results = new Queue<EditorBuildExecutionResult>(results);
        }

        /// <summary>
        /// Executes one queued build item and returns the next configured result.
        /// </summary>
        /// <param name="queueItem">Queued build item currently being processed.</param>
        /// <returns>Next configured execution result.</returns>
        public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            ExecutedQueueItemIds.Add(queueItem.QueueItemId);
            ObservedStatuses.Add(queueItem.Status);

            if (Results.Count == 0) {
                return EditorBuildExecutionResult.Success("Executed.");
            }

            return Results.Dequeue();
        }
    }
}
