namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one required runtime feature together with the authored source that caused the requirement.
/// </summary>
public sealed class PlatformBuildRequiredRuntimeFeature {
    /// <summary>
    /// Initializes one required runtime feature record.
    /// </summary>
    /// <param name="featureId">The stable runtime feature identifier that must remain enabled.</param>
    /// <param name="sourceKind">The authored source category that required the feature.</param>
    /// <param name="sourceId">The stable source identity that caused the requirement.</param>
    /// <param name="reason">The human-readable explanation for the requirement.</param>
    /// <exception cref="ArgumentException">Thrown when a required string value is missing.</exception>
    public PlatformBuildRequiredRuntimeFeature(
        string featureId,
        RuntimeFeatureRequirementSourceKind sourceKind,
        string sourceId,
        string reason) {
        if (string.IsNullOrWhiteSpace(featureId)) {
            throw new ArgumentException("Runtime feature id is required.", nameof(featureId));
        } else if (string.IsNullOrWhiteSpace(sourceId)) {
            throw new ArgumentException("Runtime feature source id is required.", nameof(sourceId));
        } else if (string.IsNullOrWhiteSpace(reason)) {
            throw new ArgumentException("Runtime feature requirement reason is required.", nameof(reason));
        }

        FeatureId = featureId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the stable runtime feature identifier that must remain enabled.
    /// </summary>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the authored source category that required the feature.
    /// </summary>
    public RuntimeFeatureRequirementSourceKind SourceKind { get; }

    /// <summary>
    /// Gets the stable source identity that caused the requirement.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Gets the human-readable explanation for the requirement.
    /// </summary>
    public string Reason { get; }
}
