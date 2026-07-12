namespace helengine.editor {
    /// <summary>
    /// Describes an editor scene asset resolver that can track the runtime assets materialized during one scene-load scope so the editor can release them when the scene is replaced.
    /// </summary>
    public interface IEditorOwnedAssetTrackingSceneAssetReferenceResolver {
        /// <summary>
        /// Starts one editor scene-owned asset tracking scope.
        /// </summary>
        void BeginOwnedAssetTracking();

        /// <summary>
        /// Completes the active editor scene-owned asset tracking scope and returns the assets that were materialized for the scene.
        /// </summary>
        /// <returns>Scene-owned runtime assets resolved during the active editor scene-load scope.</returns>
        RuntimeSceneOwnedAssetSet CompleteOwnedAssetTracking();

        /// <summary>
        /// Cancels the active editor scene-owned asset tracking scope and returns the assets that were materialized before the load failed.
        /// </summary>
        /// <returns>Scene-owned runtime assets that were materialized before the load failed.</returns>
        RuntimeSceneOwnedAssetSet CancelOwnedAssetTracking();
    }
}
