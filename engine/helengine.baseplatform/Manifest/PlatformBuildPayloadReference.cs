namespace helengine.baseplatform.Manifest;

/// <summary>
/// Identifies a resolved payload input that the builder should transform into a platform-readable output.
/// </summary>
public class PlatformBuildPayloadReference {
    /// <summary>
    /// Initializes a new payload reference with a stable logical id and its source identity.
    /// </summary>
    /// <param name="logicalId">The stable logical id used by the manifest producer for this payload.</param>
    /// <param name="sourceIdentity">The source identity or path for the payload on disk.</param>
    /// <exception cref="ArgumentException">Thrown when either required value is missing.</exception>
    public PlatformBuildPayloadReference(string logicalId, string sourceIdentity) {
        if (string.IsNullOrWhiteSpace(logicalId)) {
            throw new ArgumentException("Payload logical id is required.", nameof(logicalId));
        } else if (string.IsNullOrWhiteSpace(sourceIdentity)) {
            throw new ArgumentException("Payload source identity is required.", nameof(sourceIdentity));
        }

        LogicalId = logicalId;
        SourceIdentity = sourceIdentity;
    }

    /// <summary>
    /// Gets the stable logical id used by the manifest producer for this payload.
    /// </summary>
    public string LogicalId { get; }

    /// <summary>
    /// Gets the source identity or path for the payload on disk.
    /// </summary>
    public string SourceIdentity { get; }
}
