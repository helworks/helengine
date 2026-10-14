namespace helengine.editor.tests.testing {
    /// <summary>
    /// Play asset-cook executor for runner tests that reports every cook as failed without processing any content.
    /// </summary>
    public sealed class FailingPlayAssetCookExecutor : IEditorPlayAssetCookExecutor {
        /// <summary>
        /// Reports a failed cook.
        /// </summary>
        /// <param name="queueItem">Play queue item that would have been cooked.</param>
        /// <returns>Failed cook result.</returns>
        public EditorPlayCookResult ExecuteAssetsOnly(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            return EditorPlayCookResult.Failure("The test cook failed.");
        }
    }
}
