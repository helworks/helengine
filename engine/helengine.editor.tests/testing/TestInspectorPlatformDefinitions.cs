using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Supplies explicit installed-platform metadata to inspector tests that exercise platform editing.
    /// </summary>
    internal static class TestInspectorPlatformDefinitions {
        /// <summary>
        /// Creates minimal definitions for platforms whose ordinary component fields the test will edit.
        /// </summary>
        /// <param name="platformIds">Platforms considered installed in the test.</param>
        /// <returns>Definitions keyed by the supplied platform identifiers.</returns>
        public static IReadOnlyDictionary<string, PlatformDefinition> Create(params string[] platformIds) {
            Dictionary<string, PlatformDefinition> definitions = new Dictionary<string, PlatformDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (string platformId in platformIds) {
                definitions.Add(platformId, new PlatformDefinition(platformId, platformId, [], [], [], [], [], [], [], []));
            }
            return definitions;
        }
    }
}
