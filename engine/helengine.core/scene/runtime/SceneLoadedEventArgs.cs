namespace helengine {
    /// <summary>
    /// Carries scene metadata for a post-load notification.
    /// </summary>
    public sealed class SceneLoadedEventArgs {
        /// <summary>
        /// Initializes one scene-loaded event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that was loaded.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path that was materialized.</param>
        /// <param name="rootEntities">Tracked root entities materialized from the scene payload.</param>
        public SceneLoadedEventArgs(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
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
        /// Gets the stable scene identifier that was loaded.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path that was materialized.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities materialized from the scene payload.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
