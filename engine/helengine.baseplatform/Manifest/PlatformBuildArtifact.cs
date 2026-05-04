namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one cooked runtime artifact produced by the editor-owned build graph.
/// </summary>
public sealed class PlatformBuildArtifact {
    /// <summary>
    /// Initializes one cooked artifact entry.
    /// </summary>
    public PlatformBuildArtifact(
        string relativePath,
        string logicalArtifactId,
        string contentHash,
        string artifactKind,
        string variantId) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Artifact relative path is required.", nameof(relativePath));
        } else if (string.IsNullOrWhiteSpace(logicalArtifactId)) {
            throw new ArgumentException("Artifact logical id is required.", nameof(logicalArtifactId));
        } else if (string.IsNullOrWhiteSpace(contentHash)) {
            throw new ArgumentException("Artifact content hash is required.", nameof(contentHash));
        } else if (string.IsNullOrWhiteSpace(artifactKind)) {
            throw new ArgumentException("Artifact kind is required.", nameof(artifactKind));
        } else if (string.IsNullOrWhiteSpace(variantId)) {
            throw new ArgumentException("Artifact variant id is required.", nameof(variantId));
        }

        RelativePath = relativePath;
        LogicalArtifactId = logicalArtifactId;
        ContentHash = contentHash;
        ArtifactKind = artifactKind;
        VariantId = variantId;
    }

    /// <summary>
    /// Initializes one cooked artifact entry using the relative path as its logical id.
    /// </summary>
    public PlatformBuildArtifact(
        string relativePath,
        string contentHash,
        string artifactKind,
        string variantId)
        : this(relativePath, relativePath, contentHash, artifactKind, variantId) {
    }

    /// <summary>
    /// Gets the runtime-relative artifact path.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the stable logical artifact id.
    /// </summary>
    public string LogicalArtifactId { get; }

    /// <summary>
    /// Gets the stable content hash for deduplication and sharing.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    /// Gets the logical artifact kind.
    /// </summary>
    public string ArtifactKind { get; }

    /// <summary>
    /// Gets the stable logical variant id or sharing bucket.
    /// </summary>
    public string VariantId { get; }
}
