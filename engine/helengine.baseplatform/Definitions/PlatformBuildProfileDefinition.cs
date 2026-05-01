namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one build profile exposed by a platform builder to the editor.
/// </summary>
public class PlatformBuildProfileDefinition {
    /// <summary>
    /// Initializes one build profile definition.
    /// </summary>
    /// <param name="profileId">Stable profile identifier.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="description">Short description of the profile.</param>
    /// <param name="graphicsProfileId">Graphics profile selected by default for this build profile.</param>
    /// <param name="settings">Platform-specific settings exposed by this build profile.</param>
    public PlatformBuildProfileDefinition(
        string profileId,
        string displayName,
        string description,
        string graphicsProfileId,
        PlatformSettingDefinition[] settings) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Build profile id is required.", nameof(profileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Build profile display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(description)) {
            throw new ArgumentException("Build profile description is required.", nameof(description));
        } else if (string.IsNullOrWhiteSpace(graphicsProfileId)) {
            throw new ArgumentException("Build profile graphics profile id is required.", nameof(graphicsProfileId));
        } else if (settings == null) {
            throw new ArgumentNullException(nameof(settings), "Build profile settings are required.");
        } else if (Array.Exists(settings, setting => setting == null)) {
            throw new ArgumentException("Build profile settings cannot contain null entries.", nameof(settings));
        }

        ProfileId = profileId;
        DisplayName = displayName;
        Description = description;
        GraphicsProfileId = graphicsProfileId;
        Settings = [.. settings];
    }

    /// <summary>
    /// Gets the stable profile identifier.
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the short description of the profile.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the graphics profile selected by default for this build profile.
    /// </summary>
    public string GraphicsProfileId { get; }

    /// <summary>
    /// Gets the platform-specific settings exposed by this build profile.
    /// </summary>
    public PlatformSettingDefinition[] Settings { get; }
}
