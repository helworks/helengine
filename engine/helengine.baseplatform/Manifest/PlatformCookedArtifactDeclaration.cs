namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes the explicit identity of one material or shader file already written into the cooked platform output.
/// </summary>
public sealed class PlatformCookedArtifactDeclaration {
    /// <summary>
    /// Initializes one explicit cooked material or shader artifact declaration.
    /// </summary>
    /// <param name="relativePath">Runtime-relative path of the already-written cooked file.</param>
    /// <param name="logicalArtifactId">Stable producer-defined identity of the artifact.</param>
    /// <param name="artifactKind">Declared artifact kind, limited to material or shader.</param>
    /// <param name="variantId">Stable platform or sharing variant that owns the file.</param>
    public PlatformCookedArtifactDeclaration(
        string relativePath,
        string logicalArtifactId,
        string artifactKind,
        string variantId) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Artifact relative path is required.", nameof(relativePath));
        } else if (string.IsNullOrWhiteSpace(logicalArtifactId)) {
            throw new ArgumentException("Artifact logical id is required.", nameof(logicalArtifactId));
        } else if (!string.Equals(artifactKind, "material", StringComparison.Ordinal)
            && !string.Equals(artifactKind, "shader", StringComparison.Ordinal)) {
            throw new ArgumentException("Artifact kind must be either 'material' or 'shader'.", nameof(artifactKind));
        } else if (string.IsNullOrWhiteSpace(variantId)) {
            throw new ArgumentException("Artifact variant id is required.", nameof(variantId));
        }

        RelativePath = relativePath.Replace('\\', '/');
        LogicalArtifactId = logicalArtifactId;
        ArtifactKind = artifactKind;
        VariantId = variantId;
    }

    /// <summary>
    /// Gets the normalized runtime-relative file path of the cooked artifact.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the stable logical identity assigned by the material or shader producer.
    /// </summary>
    public string LogicalArtifactId { get; }

    /// <summary>
    /// Gets the producer-declared artifact kind.
    /// </summary>
    public string ArtifactKind { get; }

    /// <summary>
    /// Gets the stable platform or sharing variant that owns the artifact.
    /// </summary>
    public string VariantId { get; }
}
