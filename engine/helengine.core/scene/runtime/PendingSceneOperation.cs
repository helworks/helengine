namespace helengine {
    /// <summary>
    /// Stores one deferred runtime scene operation requested during the active object-manager update sweep.
    /// </summary>
    public sealed class PendingSceneOperation {
        /// <summary>
        /// Initializes one deferred runtime scene operation.
        /// </summary>
        /// <param name="operationKind">Kind of scene operation to execute after the update loop.</param>
        /// <param name="sceneId">Stable scene identifier the operation targets.</param>
        /// <param name="loadMode">Requested load mode when the operation loads a scene.</param>
        PendingSceneOperation(PendingSceneOperationKind operationKind, string sceneId, SceneLoadMode loadMode) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            OperationKind = operationKind;
            SceneId = sceneId;
            LoadMode = loadMode;
        }

        /// <summary>
        /// Gets the deferred scene operation kind.
        /// </summary>
        public PendingSceneOperationKind OperationKind { get; }

        /// <summary>
        /// Gets the stable scene identifier targeted by the deferred operation.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the requested runtime load mode when the deferred operation loads a scene.
        /// </summary>
        public SceneLoadMode LoadMode { get; }

        /// <summary>
        /// Creates one deferred runtime scene-load operation.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to load.</param>
        /// <param name="loadMode">Runtime load mode requested by the caller.</param>
        /// <returns>Deferred scene-load operation.</returns>
        public static PendingSceneOperation CreateLoad(string sceneId, SceneLoadMode loadMode) {
            return new PendingSceneOperation(PendingSceneOperationKind.Load, sceneId, loadMode);
        }

        /// <summary>
        /// Creates one deferred runtime scene-unload operation.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier to unload.</param>
        /// <returns>Deferred scene-unload operation.</returns>
        public static PendingSceneOperation CreateUnload(string sceneId) {
            return new PendingSceneOperation(PendingSceneOperationKind.Unload, sceneId, SceneLoadMode.Single);
        }
    }
}
