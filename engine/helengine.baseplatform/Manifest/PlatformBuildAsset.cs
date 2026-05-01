namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes a fully resolved loose asset that must be cooked outside scene packaging.
/// </summary>
public class PlatformBuildAsset {
    /// <summary>
    /// Initializes a loose asset entry with its source identity, payload, and resolved metadata.
    /// </summary>
    /// <param name="assetId">The stable logical asset id.</param>
    /// <param name="assetName">The display name of the asset.</param>
    /// <param name="sourceIdentity">The source identity or path for the asset definition.</param>
    /// <param name="payloadReference">The resolved payload the builder should transform.</param>
    /// <param name="resolvedMetadata">The resolved metadata already prepared by the caller for this asset.</param>
    /// <exception cref="ArgumentException">Thrown when a required string value is missing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the payload reference or metadata collection is missing.</exception>
    public PlatformBuildAsset(
        string assetId,
        string assetName,
        string sourceIdentity,
        PlatformBuildPayloadReference payloadReference,
        KeyValuePair<string, string>[] resolvedMetadata) {
        if (string.IsNullOrWhiteSpace(assetId)) {
            throw new ArgumentException("Asset id is required.", nameof(assetId));
        } else if (string.IsNullOrWhiteSpace(assetName)) {
            throw new ArgumentException("Asset name is required.", nameof(assetName));
        } else if (string.IsNullOrWhiteSpace(sourceIdentity)) {
            throw new ArgumentException("Asset source identity is required.", nameof(sourceIdentity));
        } else if (payloadReference == null) {
            throw new ArgumentNullException(nameof(payloadReference), "Asset payload reference is required.");
        } else if (resolvedMetadata == null) {
            throw new ArgumentNullException(nameof(resolvedMetadata), "Asset metadata is required.");
        }

        AssetId = assetId;
        AssetName = assetName;
        SourceIdentity = sourceIdentity;
        PayloadReference = payloadReference;
        ResolvedMetadata = [.. resolvedMetadata];
    }

    /// <summary>
    /// Gets the stable logical asset id.
    /// </summary>
    public string AssetId { get; }

    /// <summary>
    /// Gets the display name of the asset.
    /// </summary>
    public string AssetName { get; }

    /// <summary>
    /// Gets the source identity or path for the asset definition.
    /// </summary>
    public string SourceIdentity { get; }

    /// <summary>
    /// Gets the resolved payload the builder should transform.
    /// </summary>
    public PlatformBuildPayloadReference PayloadReference { get; }

    /// <summary>
    /// Gets the resolved metadata already prepared by the caller for this asset.
    /// </summary>
    public KeyValuePair<string, string>[] ResolvedMetadata { get; }
}
