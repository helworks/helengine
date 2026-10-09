namespace helengine.timeline {
    /// <summary>
    /// Loads timelines referenced by nested timeline clips, so validation (and later cooking) can follow asset references,
    /// check slot mappings and detect reference cycles. The editor resolves against the project; tests use dictionaries.
    /// </summary>
    public interface ITimelineAssetResolver {
        /// <summary>
        /// Loads the timeline an asset reference points at.
        /// </summary>
        /// <param name="reference">Reference stored on a nested timeline clip.</param>
        /// <returns>The referenced timeline, or null when nothing can be found at that reference.</returns>
        TimelineAsset Resolve(SceneAssetReference reference);
    }
}
