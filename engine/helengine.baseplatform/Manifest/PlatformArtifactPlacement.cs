namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes where a cooked artifact variant is physically placed in the runtime container layout.
/// </summary>
public sealed class PlatformArtifactPlacement {
    /// <summary>
    /// Initializes one placement entry.
    /// </summary>
    public PlatformArtifactPlacement(
        string logicalArtifactId,
        string variantId,
        string containerId,
        long offsetBytes,
        long lengthBytes,
        int repeatIndex,
        int placementPriority) {
        if (string.IsNullOrWhiteSpace(logicalArtifactId)) {
            throw new ArgumentException("Logical artifact id is required.", nameof(logicalArtifactId));
        } else if (string.IsNullOrWhiteSpace(variantId)) {
            throw new ArgumentException("Variant id is required.", nameof(variantId));
        } else if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container id is required.", nameof(containerId));
        } else if (offsetBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(offsetBytes), "Offset bytes cannot be negative.");
        } else if (lengthBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(lengthBytes), "Length bytes cannot be negative.");
        } else if (repeatIndex < 0) {
            throw new ArgumentOutOfRangeException(nameof(repeatIndex), "Repeat index cannot be negative.");
        } else if (placementPriority < 0) {
            throw new ArgumentOutOfRangeException(nameof(placementPriority), "Placement priority cannot be negative.");
        }

        LogicalArtifactId = logicalArtifactId;
        VariantId = variantId;
        ContainerId = containerId;
        OffsetBytes = offsetBytes;
        LengthBytes = lengthBytes;
        RepeatIndex = repeatIndex;
        PlacementPriority = placementPriority;
    }

    /// <summary>
    /// Gets the stable logical artifact id being placed.
    /// </summary>
    public string LogicalArtifactId { get; }

    /// <summary>
    /// Gets the selected cooked variant id.
    /// </summary>
    public string VariantId { get; }

    /// <summary>
    /// Gets the target container id.
    /// </summary>
    public string ContainerId { get; }

    /// <summary>
    /// Gets the byte offset within the container.
    /// </summary>
    public long OffsetBytes { get; }

    /// <summary>
    /// Gets the byte length of the placed range.
    /// </summary>
    public long LengthBytes { get; }

    /// <summary>
    /// Gets the physical repeat index for locality-aware duplication.
    /// </summary>
    public int RepeatIndex { get; }

    /// <summary>
    /// Gets the relative placement priority.
    /// </summary>
    public int PlacementPriority { get; }
}
