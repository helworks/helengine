namespace helengine {
    /// <summary>
    /// Stores the runtime bookkeeping for one currently loaded built scene.
    /// </summary>
    public sealed class LoadedSceneRecord {
        /// <summary>
        /// Initializes one loaded-scene record.
        /// </summary>
        /// <param name="sceneId">Stable built scene identifier.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
        /// <param name="rootEntities">Tracked root entities materialized from the scene payload.</param>
        public LoadedSceneRecord(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            SceneId = sceneId;
            CookedRelativePath = cookedRelativePath;
            RootEntities = rootEntities;
        }

        /// <summary>
        /// Gets the stable built scene identifier.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities materialized from the scene payload.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
