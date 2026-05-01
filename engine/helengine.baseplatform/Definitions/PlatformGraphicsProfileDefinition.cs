namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one graphics profile exposed by a platform builder to the editor.
/// </summary>
public class PlatformGraphicsProfileDefinition {
    /// <summary>
    /// Initializes one graphics profile definition.
    /// </summary>
    /// <param name="profileId">Stable graphics profile identifier.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="description">Short description of the graphics profile.</param>
    /// <param name="settings">Platform-specific graphics settings exposed by the profile.</param>
    public PlatformGraphicsProfileDefinition(
        string profileId,
        string displayName,
        string description,
        PlatformSettingDefinition[] settings) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Graphics profile id is required.", nameof(profileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Graphics profile display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(description)) {
            throw new ArgumentException("Graphics profile description is required.", nameof(description));
        } else if (settings == null) {
            throw new ArgumentNullException(nameof(settings), "Graphics profile settings are required.");
        } else if (Array.Exists(settings, setting => setting == null)) {
            throw new ArgumentException("Graphics profile settings cannot contain null entries.", nameof(settings));
        }

        ProfileId = profileId;
        DisplayName = displayName;
        Description = description;
        Settings = [.. settings];
    }

    /// <summary>
    /// Gets the stable graphics profile identifier.
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the short description of the graphics profile.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the platform-specific graphics settings exposed by the profile.
    /// </summary>
    public PlatformSettingDefinition[] Settings { get; }
}
