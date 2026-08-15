using Xunit;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Serializes tests that mutate the process-wide editor build-cache environment.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class EditorBuildCacheEnvironmentCollection {
        /// <summary>
        /// Stable xUnit collection name shared by editor build-cache tests.
        /// </summary>
        public const string Name = "Editor build cache environment";
    }
}
