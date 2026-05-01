namespace helengine.editor {
    /// <summary>
    /// Routes queued build items to the executor that owns their platform family.
    /// </summary>
    public sealed class EditorBuildExecutorRouter : IEditorBuildExecutor {
        /// <summary>
        /// Lookup table keyed by platform identifier.
        /// </summary>
        readonly IReadOnlyDictionary<string, IEditorBuildExecutor> ExecutorsByPlatformId;

        /// <summary>
        /// Initializes one build executor router for the supplied platform executors.
        /// </summary>
        /// <param name="executorsByPlatformId">Executors keyed by the platform identifier they own.</param>
        public EditorBuildExecutorRouter(IReadOnlyDictionary<string, IEditorBuildExecutor> executorsByPlatformId) {
            if (executorsByPlatformId == null) {
                throw new ArgumentNullException(nameof(executorsByPlatformId));
            }

            ExecutorsByPlatformId = executorsByPlatformId;
        }

        /// <summary>
        /// Dispatches one queued build item to the executor registered for its platform id.
        /// </summary>
        /// <param name="queueItem">Queued build item that should be executed.</param>
        /// <returns>The routed executor result, or a failure when no executor exists for the platform.</returns>
        public EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            if (string.IsNullOrWhiteSpace(queueItem.PlatformId)) {
                return EditorBuildExecutionResult.Failure("Queued build item did not provide a platform id.");
            }

            if (!ExecutorsByPlatformId.TryGetValue(queueItem.PlatformId, out IEditorBuildExecutor executor) || executor == null) {
                return EditorBuildExecutionResult.Failure($"No build executor is registered for platform '{queueItem.PlatformId}'.");
            }

            return executor.Execute(queueItem);
        }
    }
}
