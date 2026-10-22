using helengine.baseplatform.Builders;

namespace helengine.editor.tests.testing {
    /// <summary>Loads supplied platform instances through the real catalog without assembling temporary plugin DLLs.</summary>
    public sealed class TestCookContextBuilderLoader : EditorPlatformAssetBuilderLoader {
        /// <summary>Retains immutable test routing keyed by the supplied descriptor paths.</summary>
        readonly IReadOnlyDictionary<string, IPlatformAssetBuilder> Builders;
        /// <summary>Captures the explicit builders that the real catalog should load.</summary>
        public TestCookContextBuilderLoader(IReadOnlyDictionary<string, IPlatformAssetBuilder> builders) { Builders = builders; }
        /// <summary>Resolves a preselected builder without changing real catalog registration behavior.</summary>
        public override IPlatformAssetBuilder Load(string assemblyPath) => Builders[assemblyPath];
    }
}
