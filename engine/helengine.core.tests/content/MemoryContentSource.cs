namespace helengine.core.tests.content {
    /// <summary>
    /// In-memory content stream source that serves fixed byte payloads by exact asset path.
    /// </summary>
    public sealed class MemoryContentSource : IContentStreamSource {
        /// <summary>
        /// Creates a source that serves the supplied payloads.
        /// </summary>
        /// <param name="contents">Payload bytes keyed by exact runtime asset path.</param>
        public MemoryContentSource(Dictionary<string, byte[]> contents) {
            if (contents == null) {
                throw new ArgumentNullException(nameof(contents));
            }
            Contents = contents;
        }

        /// <summary>
        /// Gets the payload bytes keyed by exact runtime asset path.
        /// </summary>
        public Dictionary<string, byte[]> Contents { get; }

        /// <summary>
        /// Opens a read-only stream over the payload registered for the path.
        /// </summary>
        /// <param name="assetPath">Exact runtime asset path.</param>
        /// <returns>Read-only memory stream over the registered payload.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no payload is registered for the path.</exception>
        public Stream OpenRead(string assetPath) {
            if (Contents.TryGetValue(assetPath, out byte[] bytes)) {
                return new MemoryStream(bytes, false);
            }
            throw new FileNotFoundException("No in-memory content is registered for the asset path.", assetPath);
        }
    }
}
