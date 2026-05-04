using helengine.baseplatform.Profiles;

namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one code generation profile exposed by a platform builder to the editor.
/// </summary>
public sealed class PlatformCodegenProfileDefinition {
    /// <summary>
    /// Initializes one codegen profile definition.
    /// </summary>
    /// <param name="profileId">Stable codegen profile identifier.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="description">Short description of the codegen profile.</param>
    /// <param name="outputLanguage">The emitted output language.</param>
    /// <param name="endianness">The byte order used for emitted cooked data.</param>
    /// <param name="settings">Codegen-specific settings exposed by the profile.</param>
    public PlatformCodegenProfileDefinition(
        string profileId,
        string displayName,
        string description,
        PlatformCodegenLanguage outputLanguage,
        PlatformSerializationEndianness endianness,
        PlatformSettingDefinition[] settings) {
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Codegen profile id is required.", nameof(profileId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Codegen profile display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(description)) {
            throw new ArgumentException("Codegen profile description is required.", nameof(description));
        } else if (settings == null) {
            throw new ArgumentNullException(nameof(settings), "Codegen profile settings are required.");
        } else if (Array.Exists(settings, setting => setting == null)) {
            throw new ArgumentException("Codegen profile settings cannot contain null entries.", nameof(settings));
        }

        ProfileId = profileId;
        DisplayName = displayName;
        Description = description;
        OutputLanguage = outputLanguage;
        Endianness = endianness;
        Settings = [.. settings];
    }

    /// <summary>
    /// Gets the stable codegen profile identifier.
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the short description of the codegen profile.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the emitted output language.
    /// </summary>
    public PlatformCodegenLanguage OutputLanguage { get; }

    /// <summary>
    /// Gets the byte order used for emitted cooked data.
    /// </summary>
    public PlatformSerializationEndianness Endianness { get; }

    /// <summary>
    /// Gets the codegen-specific settings exposed by the profile.
    /// </summary>
    public PlatformSettingDefinition[] Settings { get; }
}
