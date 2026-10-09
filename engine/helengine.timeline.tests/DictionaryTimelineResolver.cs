namespace helengine.timeline.tests {
    /// <summary>
    /// Resolves timeline references from an in-memory table keyed by relative path, standing in for a project.
    /// </summary>
    sealed class DictionaryTimelineResolver : ITimelineAssetResolver {
        /// <summary>
        /// Timelines by relative path.
        /// </summary>
        readonly Dictionary<string, TimelineAsset> Timelines = new Dictionary<string, TimelineAsset>(StringComparer.Ordinal);

        /// <summary>
        /// Registers one timeline under a relative path.
        /// </summary>
        /// <param name="relativePath">Path references use.</param>
        /// <param name="timeline">Timeline returned for that path.</param>
        public void Add(string relativePath, TimelineAsset timeline) {
            Timelines.Add(relativePath, timeline);
        }

        /// <summary>
        /// Returns the timeline registered under the reference's path, or null.
        /// </summary>
        /// <param name="reference">Reference to resolve.</param>
        /// <returns>The registered timeline, or null.</returns>
        public TimelineAsset Resolve(SceneAssetReference reference) {
            TimelineAsset timeline;
            return Timelines.TryGetValue(reference.RelativePath, out timeline) ? timeline : null;
        }
    }
}
