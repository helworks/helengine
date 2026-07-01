using helengine.baseplatform.Builders;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a deterministic platform builder loader for editor catalog tests.
    /// </summary>
    public sealed class TestEditorPlatformAssetBuilderLoader : EditorPlatformAssetBuilderLoader {
        /// <summary>
        /// Builder instances keyed by the assembly paths requested by the editor.
        /// </summary>
        readonly IReadOnlyDictionary<string, IPlatformAssetBuilder> BuildersByAssemblyPath;

        /// <summary>
        /// Tracks assembly paths requested through the test loader.
        /// </summary>
        readonly List<string> RecordedAssemblyPaths;

        /// <summary>
        /// Initializes one test builder loader with explicit assembly-path mappings.
        /// </summary>
        /// <param name="buildersByAssemblyPath">Builder instances keyed by the assembly paths the test expects to load.</param>
        public TestEditorPlatformAssetBuilderLoader(IReadOnlyDictionary<string, IPlatformAssetBuilder> buildersByAssemblyPath) {
            BuildersByAssemblyPath = buildersByAssemblyPath ?? throw new ArgumentNullException(nameof(buildersByAssemblyPath));
            RecordedAssemblyPaths = [];
        }

        /// <summary>
        /// Gets the ordered assembly paths requested through the loader.
        /// </summary>
        public IReadOnlyList<string> LoadedAssemblyPaths => RecordedAssemblyPaths;

        /// <summary>
        /// Returns the preconfigured builder for the requested assembly path and records the request order.
        /// </summary>
        /// <param name="assemblyPath">Assembly path supplied by the editor catalog.</param>
        /// <returns>Preconfigured platform builder instance.</returns>
        public override IPlatformAssetBuilder Load(string assemblyPath) {
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Assembly path must be provided.", nameof(assemblyPath));
            }

            if (!BuildersByAssemblyPath.TryGetValue(assemblyPath, out IPlatformAssetBuilder builder)) {
                throw new InvalidOperationException($"No test builder is configured for assembly path '{assemblyPath}'.");
            }

            RecordedAssemblyPaths.Add(assemblyPath);
            return builder;
        }
    }
}
