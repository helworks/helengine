namespace helengine.editor {
    /// <summary>
    /// Runs the assets-only part of a platform build for one Play queue item: content processing and staging,
    /// without native compilation or packaging. <see cref="EditorPlatformBuildExecutor"/> is the editor's
    /// implementation; <see cref="EditorPlayCookRunner"/> depends on this contract so the runner can be exercised
    /// without a loaded platform builder.
    /// </summary>
    public interface IEditorPlayAssetCookExecutor {
        /// <summary>
        /// Cooks and stages the content selected by the queue item.
        /// </summary>
        /// <param name="queueItem">Play queue item naming the platform settings and the scene to cook.</param>
        /// <returns>Cook outcome with the staged content root and startup scene on success.</returns>
        EditorPlayCookResult ExecuteAssetsOnly(EditorBuildQueueItemDocument queueItem);
    }
}
