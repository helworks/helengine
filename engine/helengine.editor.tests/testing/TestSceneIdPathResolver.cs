namespace helengine.editor.tests.testing {
    /// <summary>
    /// Deterministic scene-id path resolver used by editor-mode menu scene-loading tests.
    /// </summary>
    internal sealed class TestSceneIdPathResolver : ISceneIdPathResolver {
        /// <summary>
        /// Stable scene-id mappings returned by the resolver.
        /// </summary>
        readonly IReadOnlyDictionary<string, string> ScenePathsById;

        /// <summary>
        /// Initializes one resolver backed by the supplied stable scene-id mappings.
        /// </summary>
        /// <param name="scenePathsById">Stable scene-id mappings to expose.</param>
        public TestSceneIdPathResolver(IReadOnlyDictionary<string, string> scenePathsById) {
            ScenePathsById = scenePathsById ?? throw new ArgumentNullException(nameof(scenePathsById));
            LastResolvedSceneId = string.Empty;
        }

        /// <summary>
        /// Gets the most recent scene id resolved by the test resolver.
        /// </summary>
        public string LastResolvedSceneId { get; private set; }

        /// <summary>
        /// Gets the number of successful resolution calls performed by the resolver.
        /// </summary>
        public int ResolveCallCount { get; private set; }

        /// <summary>
        /// Resolves one authored scene path from the supplied stable scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <returns>Authored scene path relative to the active content root.</returns>
        public string ResolveScenePath(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (!ScenePathsById.TryGetValue(sceneId, out string scenePath) || string.IsNullOrWhiteSpace(scenePath)) {
                throw new InvalidOperationException($"Test resolver does not contain a scene path for scene id '{sceneId}'.");
            }

            LastResolvedSceneId = sceneId;
            ResolveCallCount++;
            return scenePath;
        }
    }
}
