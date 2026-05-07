namespace helengine {
    /// <summary>
    /// Carries scene metadata for a pre-unload notification.
    /// </summary>
    public sealed class SceneUnloadingEventArgs {
        /// <summary>
        /// Initializes one scene-unloading event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that is about to be unloaded.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path that is being removed.</param>
        /// <param name="rootEntities">Tracked root entities that the player must destroy.</param>
        public SceneUnloadingEventArgs(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities) {
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
        /// Gets the stable scene identifier that is about to be unloaded.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path that is being removed.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Gets the tracked root entities that the player must destroy.
        /// </summary>
        public IReadOnlyList<Entity> RootEntities { get; }
    }
}
