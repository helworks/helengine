namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes all runtime features the editor determined must remain enabled for one build output.
/// </summary>
public sealed class PlatformBuildRuntimeFeatureManifest {
    /// <summary>
    /// Represents one manifest with no required runtime features.
    /// </summary>
    public static PlatformBuildRuntimeFeatureManifest Empty { get; } = new(Array.Empty<PlatformBuildRequiredRuntimeFeature>());

    /// <summary>
    /// Initializes one runtime feature manifest.
    /// </summary>
    /// <param name="requiredFeatures">The ordered required runtime features together with their provenance.</param>
    /// <exception cref="ArgumentNullException">Thrown when the requirement collection is missing.</exception>
    /// <exception cref="ArgumentException">Thrown when the requirement collection contains a missing entry.</exception>
    public PlatformBuildRuntimeFeatureManifest(PlatformBuildRequiredRuntimeFeature[] requiredFeatures) {
        if (requiredFeatures == null) {
            throw new ArgumentNullException(nameof(requiredFeatures), "Runtime feature requirement collection is required.");
        } else if (Array.Exists(requiredFeatures, requirement => requirement == null)) {
            throw new ArgumentException("Runtime feature requirement collection cannot contain null entries.", nameof(requiredFeatures));
        }

        RequiredFeatures = [.. requiredFeatures];
    }

    /// <summary>
    /// Gets the ordered required runtime features together with their provenance.
    /// </summary>
    public PlatformBuildRequiredRuntimeFeature[] RequiredFeatures { get; }
}
