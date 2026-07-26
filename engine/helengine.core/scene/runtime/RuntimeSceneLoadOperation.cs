namespace helengine {
    /// <summary>
    /// Incrementally materializes one packaged scene so the runtime can publish real progress between frame boundaries.
    /// </summary>
    public sealed class RuntimeSceneLoadOperation {
        /// <summary>
        /// Service that resolves scene entities and owns asset-tracking lifecycle operations.
        /// </summary>
        readonly RuntimeSceneLoadService SceneLoadService;

        /// <summary>
        /// Serialized root entities awaiting materialization.
        /// </summary>
        readonly SceneEntityAsset[] RootEntityAssets;

        /// <summary>
        /// Runtime roots materialized by completed advances.
        /// </summary>
        readonly List<Entity> RootEntities;

        /// <summary>
        /// Index of the next serialized root entity to materialize.
        /// </summary>
        int NextRootEntityIndex;

        /// <summary>
        /// Result made available after every root has been initialized and owned assets have been finalized.
        /// </summary>
        RuntimeSceneLoadResult ResultValue;

        /// <summary>
        /// Initializes an incremental operation for the supplied packaged scene.
        /// </summary>
        /// <param name="sceneLoadService">Service that materializes scene entities.</param>
        /// <param name="sceneAsset">Packaged scene payload to materialize.</param>
        internal RuntimeSceneLoadOperation(RuntimeSceneLoadService sceneLoadService, SceneAsset sceneAsset) {
            SceneLoadService = sceneLoadService ?? throw new ArgumentNullException(nameof(sceneLoadService));
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            RootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            RootEntities = new List<Entity>(RootEntityAssets.Length);
            SceneLoadService.BeginTrackedLoad();
        }

        /// <summary>
        /// Gets the normalized fraction of serialized root entities that have been materialized.
        /// </summary>
        public float Progress => RootEntityAssets.Length == 0
            ? (IsCompleted ? 1f : 0f)
            : (float)NextRootEntityIndex / RootEntityAssets.Length;

        /// <summary>
        /// Gets whether all roots and owned assets have been finalized.
        /// </summary>
        public bool IsCompleted => ResultValue != null;

        /// <summary>
        /// Gets the completed runtime scene payload.
        /// </summary>
        public RuntimeSceneLoadResult Result {
            get {
                if (ResultValue == null) {
                    throw new InvalidOperationException("The runtime scene load operation has not completed.");
                }

                return ResultValue;
            }
        }

        /// <summary>
        /// Materializes at most one root entity and finalizes the scene when the final root has been processed.
        /// </summary>
        public void Advance() {
            if (IsCompleted) {
                return;
            }

            if (NextRootEntityIndex < RootEntityAssets.Length) {
                RootEntities.Add(SceneLoadService.LoadRootEntity(RootEntityAssets[NextRootEntityIndex], NextRootEntityIndex));
                NextRootEntityIndex++;
            }

            if (NextRootEntityIndex == RootEntityAssets.Length) {
                for (int index = 0; index < RootEntities.Count; index++) {
                    RootEntities[index].InitializeHierarchy();
                }

                ResultValue = new RuntimeSceneLoadResult(RootEntities, SceneLoadService.CompleteTrackedLoad());
            }
        }
    }
}
