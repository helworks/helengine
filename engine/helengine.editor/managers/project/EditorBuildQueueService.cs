namespace helengine.editor {
    /// <summary>
    /// Executes persisted queued build items sequentially and rewrites their local status.
    /// </summary>
    public sealed class EditorBuildQueueService {
        /// <summary>
        /// Message written when one queued build targets a platform no longer enabled for the project.
        /// </summary>
        const string DisabledPlatformMessagePrefix = "Platform '";

        /// <summary>
        /// Persists queued build status changes for the current project.
        /// </summary>
        readonly EditorBuildConfigService BuildConfigService;

        /// <summary>
        /// Executes one queued build item.
        /// </summary>
        readonly IEditorBuildExecutor BuildExecutor;

        /// <summary>
        /// Initializes one queue service for the current project.
        /// </summary>
        /// <param name="buildConfigService">Service used to persist queue status changes.</param>
        /// <param name="buildExecutor">Executor used to run one queued build item at a time.</param>
        public EditorBuildQueueService(EditorBuildConfigService buildConfigService, IEditorBuildExecutor buildExecutor) {
            BuildConfigService = buildConfigService ?? throw new ArgumentNullException(nameof(buildConfigService));
            BuildExecutor = buildExecutor ?? throw new ArgumentNullException(nameof(buildExecutor));
        }

        /// <summary>
        /// Runs all pending queued build items in order until one item fails.
        /// </summary>
        /// <param name="buildConfig">Mutable local build configuration containing the queue items to execute.</param>
        /// <param name="supportedPlatformIds">Platforms currently enabled for the project.</param>
        public void RunPending(EditorBuildConfigDocument buildConfig, IReadOnlyList<string> supportedPlatformIds) {
            if (buildConfig == null) {
                throw new ArgumentNullException(nameof(buildConfig));
            }

            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            for (int index = 0; index < buildConfig.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = buildConfig.QueueItems[index];
                if (queueItem.Status != EditorBuildQueueItemStatus.Pending) {
                    continue;
                }

                if (!supportedPlatformIds.Contains(queueItem.PlatformId)) {
                    queueItem.Status = EditorBuildQueueItemStatus.Failed;
                    queueItem.StatusMessage = DisabledPlatformMessagePrefix + queueItem.PlatformId + "' is no longer enabled for this project.";
                    BuildConfigService.Save(buildConfig);
                    return;
                }

                queueItem.Status = EditorBuildQueueItemStatus.Running;
                queueItem.StatusMessage = string.Empty;
                BuildConfigService.Save(buildConfig);

                EditorBuildExecutionResult result = BuildExecutor.Execute(queueItem);
                if (result.Succeeded) {
                    queueItem.Status = EditorBuildQueueItemStatus.Done;
                    queueItem.StatusMessage = result.Message;
                    BuildConfigService.Save(buildConfig);
                    continue;
                }

                queueItem.Status = EditorBuildQueueItemStatus.Failed;
                queueItem.StatusMessage = result.Message;
                BuildConfigService.Save(buildConfig);
                return;
            }
        }
    }
}
