namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one media layout profile exposed by a platform builder.
/// </summary>
public sealed class PlatformMediaProfileDefinition {
    /// <summary>
    /// Initializes one media profile definition.
    /// </summary>
    public PlatformMediaProfileDefinition(
        string profileId,
        string displayName,
        PlatformMediaLayoutKind layoutKind,
        bool allowPhysicalDuplication,
        bool preferLocalityOverDeduplication) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Media profile id is required.", nameof(profileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Media profile display name is required.", nameof(displayName));
        }

        ProfileId = profileId;
        DisplayName = displayName;
        LayoutKind = layoutKind;
        AllowPhysicalDuplication = allowPhysicalDuplication;
        PreferLocalityOverDeduplication = preferLocalityOverDeduplication;
    }

    /// <summary>
    /// Gets the stable media profile identifier.
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the human-readable media profile name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the physical media layout kind.
    /// </summary>
    public PlatformMediaLayoutKind LayoutKind { get; }

    /// <summary>
    /// Gets whether the layout may duplicate files physically.
    /// </summary>
    public bool AllowPhysicalDuplication { get; }

    /// <summary>
    /// Gets whether seek locality is preferred over deduplication.
    /// </summary>
    public bool PreferLocalityOverDeduplication { get; }
}
