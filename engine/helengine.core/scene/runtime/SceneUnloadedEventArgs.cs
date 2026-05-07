namespace helengine {
    /// <summary>
    /// Carries scene metadata for a post-unload notification.
    /// </summary>
    public sealed class SceneUnloadedEventArgs {
        /// <summary>
        /// Initializes one scene-unloaded event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that was removed from tracking.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path that was removed.</param>
        public SceneUnloadedEventArgs(string sceneId, string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }

            SceneId = sceneId;
            CookedRelativePath = cookedRelativePath;
        }

        /// <summary>
        /// Gets the stable scene identifier that was removed from tracking.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path that was removed.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
