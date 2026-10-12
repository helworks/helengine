using System.IO;
using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Provides synchronous in-memory streams for legacy lifecycle tests.</summary>
    internal sealed class SceneModuleTestStreamSource : IContentStreamSource {
        /// <summary>Returns an owned sentinel stream parsed by the test scene processor.</summary>
        /// <param name="assetPath">Exact requested cooked path.</param>
        /// <returns>Owned stream.</returns>
        public Stream OpenRead(string assetPath) { return new MemoryStream(); }
    }
}
