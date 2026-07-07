namespace helengine.editor.tests.testing;

/// <summary>
/// Provides one deterministic in-memory content source for stream-based content manager tests.
/// </summary>
public sealed class FakeContentStreamSource : IContentStreamSource {
    /// <summary>
    /// Stores stream payloads keyed by requested asset path.
    /// </summary>
    readonly Dictionary<string, byte[]> PayloadsByAssetPath;

    /// <summary>
    /// Gets the ordered asset paths requested through this source.
    /// </summary>
    public List<string> RequestedAssetPaths { get; }

    /// <summary>
    /// Initializes one fake content stream source.
    /// </summary>
    public FakeContentStreamSource() {
        PayloadsByAssetPath = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        RequestedAssetPaths = new List<string>();
    }

    /// <summary>
    /// Registers one payload that should be returned when the matching asset path is opened.
    /// </summary>
    /// <param name="assetPath">Asset path that should resolve to the supplied payload.</param>
    /// <param name="payload">Payload bytes returned for the asset path.</param>
    public void Register(string assetPath, byte[] payload) {
        if (string.IsNullOrWhiteSpace(assetPath)) {
            throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
        }
        if (payload == null) {
            throw new ArgumentNullException(nameof(payload));
        }

        PayloadsByAssetPath[assetPath] = payload;
    }

    /// <summary>
    /// Opens one in-memory stream for the supplied asset path.
    /// </summary>
    /// <param name="assetPath">Asset path to open.</param>
    /// <returns>Readable memory stream for the registered payload.</returns>
    public Stream OpenRead(string assetPath) {
        if (string.IsNullOrWhiteSpace(assetPath)) {
            throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
        }

        RequestedAssetPaths.Add(assetPath);
        if (!PayloadsByAssetPath.TryGetValue(assetPath, out byte[] payload)) {
            throw new InvalidOperationException($"No fake content payload is registered for '{assetPath}'.");
        }

        return new MemoryStream(payload, false);
    }
}
