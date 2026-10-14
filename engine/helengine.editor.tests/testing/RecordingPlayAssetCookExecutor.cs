namespace helengine.editor.tests.testing {
    /// <summary>
    /// Play asset-cook executor for runner tests. It records every queue item it is asked to cook and reports a
    /// successful cook without processing any content.
    /// </summary>
    public sealed class RecordingPlayAssetCookExecutor : IEditorPlayAssetCookExecutor {
        /// <summary>
        /// Queue items received through <see cref="ExecuteAssetsOnly"/>, in call order.
        /// </summary>
        readonly List<EditorBuildQueueItemDocument> RecordedQueueItems = [];

        /// <summary>
        /// Gets the queue items received so far, in call order.
        /// </summary>
        public IReadOnlyList<EditorBuildQueueItemDocument> QueueItems => RecordedQueueItems;

        /// <summary>
        /// Records the queue item and reports a successful cook of its first scene.
        /// </summary>
        /// <param name="queueItem">Play queue item to record.</param>
        /// <returns>Successful cook result naming the queue item's output folder and first scene.</returns>
        public EditorPlayCookResult ExecuteAssetsOnly(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            RecordedQueueItems.Add(queueItem);
            return new EditorPlayCookResult(true, "Recorded.", queueItem.OutputDirectoryPath, queueItem.SelectedSceneIds[0], queueItem.SelectedSceneIds[0], new Dictionary<string, string>());
        }
    }
}
