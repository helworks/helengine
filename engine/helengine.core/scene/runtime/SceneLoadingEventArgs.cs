namespace helengine {
    /// <summary>
    /// Carries scene metadata for a pre-load notification.
    /// </summary>
    public sealed class SceneLoadingEventArgs : EventArgs {
        /// <summary>
        /// Initializes one scene-loading event payload.
        /// </summary>
        /// <param name="sceneId">Stable scene identifier that is about to be loaded.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path that will be materialized.</param>
        public SceneLoadingEventArgs(string sceneId, string cookedRelativePath) {
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
        /// Gets the stable scene identifier that is about to be loaded.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path that will be materialized.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
