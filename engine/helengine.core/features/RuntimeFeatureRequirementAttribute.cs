namespace helengine;

/// <summary>
/// Declares one generic runtime feature that becomes required when the annotated runtime type participates in a cooked build.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class RuntimeFeatureRequirementAttribute : Attribute {
    /// <summary>
    /// Initializes one runtime feature requirement declaration.
    /// </summary>
    /// <param name="featureId">Stable generic runtime feature id that becomes required when the annotated type participates in the build.</param>
    /// <param name="reason">Human-readable explanation that should appear in dependency reports.</param>
    public RuntimeFeatureRequirementAttribute(string featureId, string reason) {
        if (string.IsNullOrWhiteSpace(featureId)) {
            throw new ArgumentException("Runtime feature id is required.", nameof(featureId));
        } else if (string.IsNullOrWhiteSpace(reason)) {
            throw new ArgumentException("Runtime feature reason is required.", nameof(reason));
        }

        FeatureId = featureId;
        Reason = reason;
    }

    /// <summary>
    /// Gets the stable generic runtime feature id that becomes required when the annotated type participates in the build.
    /// </summary>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the human-readable explanation that should appear in dependency reports.
    /// </summary>
    public string Reason { get; }
}
