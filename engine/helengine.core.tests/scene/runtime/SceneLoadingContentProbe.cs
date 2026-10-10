using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Counts real content-open requests and fails before parsing so tests can observe the presentation boundary.</summary>
    internal sealed class SceneLoadingContentProbe : IContentStreamSource {
        /// <summary>Gets the number of attempted synchronous content reads.</summary>
        public int OpenCount { get; private set; }
        /// <summary>Records the content request before producing a deterministic I/O failure.</summary>
        public Stream OpenRead(string assetPath) {
            OpenCount++;
            throw new IOException("fixture read failure");
        }
    }
}
