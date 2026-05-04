namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one container that can receive cooked placements.
/// </summary>
public sealed class PlatformContainerArtifact {
    /// <summary>
    /// Initializes one container entry.
    /// </summary>
    public PlatformContainerArtifact(string containerId, string containerKind, long maxSizeBytes) {
        if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container id is required.", nameof(containerId));
        } else if (string.IsNullOrWhiteSpace(containerKind)) {
            throw new ArgumentException("Container kind is required.", nameof(containerKind));
        } else if (maxSizeBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxSizeBytes), "Max size bytes cannot be negative.");
        }

        ContainerId = containerId;
        ContainerKind = containerKind;
        MaxSizeBytes = maxSizeBytes;
    }

    /// <summary>
    /// Gets the stable container identifier.
    /// </summary>
    public string ContainerId { get; }

    /// <summary>
    /// Gets the logical container kind.
    /// </summary>
    public string ContainerKind { get; }

    /// <summary>
    /// Gets the maximum container size in bytes, or zero for unlimited.
    /// </summary>
    public long MaxSizeBytes { get; }
}
