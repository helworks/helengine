namespace helengine.baseplatform.Targets;

/// <summary>
/// Describes one requested runtime target variant in a multi-target build request.
/// </summary>
public class PlatformBuildTargetVariant {
    /// <summary>
    /// Initializes one requested target variant with explicit target and shared-cook identity.
    /// </summary>
    /// <param name="targetVariantId">The stable identifier for the requested target variant.</param>
    /// <param name="platformId">The platform family identifier that owns the target variant.</param>
    /// <param name="runtimeBackendId">The runtime backend identifier selected for the target variant.</param>
    /// <param name="cookProfileId">The cook profile identifier that groups shared cooked output.</param>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing.</exception>
    public PlatformBuildTargetVariant(
        string targetVariantId,
        string platformId,
        string runtimeBackendId,
        string cookProfileId) {
        if (string.IsNullOrWhiteSpace(targetVariantId)) {
            throw new ArgumentException("Target variant id is required.", nameof(targetVariantId));
        } else if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (string.IsNullOrWhiteSpace(runtimeBackendId)) {
            throw new ArgumentException("Runtime backend id is required.", nameof(runtimeBackendId));
        } else if (string.IsNullOrWhiteSpace(cookProfileId)) {
            throw new ArgumentException("Cook profile id is required.", nameof(cookProfileId));
        }

        TargetVariantId = targetVariantId;
        PlatformId = platformId;
        RuntimeBackendId = runtimeBackendId;
        CookProfileId = cookProfileId;
    }

    /// <summary>
    /// Gets the stable target variant identity.
    /// </summary>
    public string TargetVariantId { get; }

    /// <summary>
    /// Gets the platform family identifier that owns the target variant.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the runtime backend identifier selected for the target variant.
    /// </summary>
    public string RuntimeBackendId { get; }

    /// <summary>
    /// Gets the cook profile identifier that groups shared cooked output.
    /// </summary>
    public string CookProfileId { get; }
}
