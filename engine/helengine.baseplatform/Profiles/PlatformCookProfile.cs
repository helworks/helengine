namespace helengine.baseplatform.Profiles;

/// <summary>
/// Describes one shared cook profile used by one or more requested target variants.
/// </summary>
public class PlatformCookProfile {
    /// <summary>
    /// Initializes one cook profile with explicit identity and capability metadata.
    /// </summary>
    /// <param name="cookProfileId">The stable identifier for the cook profile.</param>
    /// <param name="displayName">The human-readable display name for the cook profile.</param>
    /// <param name="capabilities">The descriptive capability metadata associated with the profile.</param>
    /// <exception cref="ArgumentException">Thrown when any required string value is missing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the capability object is missing.</exception>
    public PlatformCookProfile(string cookProfileId, string displayName, PlatformCookProfileCapabilities capabilities) {
        if (string.IsNullOrWhiteSpace(cookProfileId)) {
            throw new ArgumentException("Cook profile id is required.", nameof(cookProfileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Cook profile display name is required.", nameof(displayName));
        } else if (capabilities == null) {
            throw new ArgumentNullException(nameof(capabilities), "Cook profile capabilities are required.");
        }

        CookProfileId = cookProfileId;
        DisplayName = displayName;
        Capabilities = capabilities;
    }

    /// <summary>
    /// Gets the stable identifier for the cook profile.
    /// </summary>
    public string CookProfileId { get; }

    /// <summary>
    /// Gets the human-readable display name for the cook profile.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the descriptive capability metadata associated with the cook profile.
    /// </summary>
    public PlatformCookProfileCapabilities Capabilities { get; }
}
