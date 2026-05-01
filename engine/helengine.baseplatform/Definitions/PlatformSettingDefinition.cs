namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one platform-specific option exposed by a build profile to the editor.
/// </summary>
public class PlatformSettingDefinition {
    /// <summary>
    /// Initializes one platform setting definition.
    /// </summary>
    /// <param name="settingId">Stable identifier for the setting.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="settingKind">The kind of value the editor should collect.</param>
    /// <param name="defaultValue">Default serialized value used when the editor has not stored an override yet.</param>
    /// <param name="required">Whether the setting must be provided before the platform can build.</param>
    /// <param name="allowedValues">Closed set of values used when the setting kind is a choice.</param>
    public PlatformSettingDefinition(
        string settingId,
        string displayName,
        PlatformSettingKind settingKind,
        string defaultValue,
        bool required,
        string[] allowedValues) {
        if (string.IsNullOrWhiteSpace(settingId)) {
            throw new ArgumentException("Setting id is required.", nameof(settingId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Setting display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(defaultValue)) {
            throw new ArgumentException("Setting default value is required.", nameof(defaultValue));
        } else if (allowedValues == null) {
            throw new ArgumentNullException(nameof(allowedValues), "Allowed values are required.");
        } else if (Array.Exists(allowedValues, allowedValue => string.IsNullOrWhiteSpace(allowedValue))) {
            throw new ArgumentException("Allowed values cannot contain blank entries.", nameof(allowedValues));
        }

        SettingId = settingId;
        DisplayName = displayName;
        SettingKind = settingKind;
        DefaultValue = defaultValue;
        Required = required;
        AllowedValues = [.. allowedValues];
    }

    /// <summary>
    /// Gets the stable identifier for the setting.
    /// </summary>
    public string SettingId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the kind of value the editor should collect.
    /// </summary>
    public PlatformSettingKind SettingKind { get; }

    /// <summary>
    /// Gets the default serialized value used when the editor has not stored an override yet.
    /// </summary>
    public string DefaultValue { get; }

    /// <summary>
    /// Gets whether the setting must be provided before the platform can build.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the closed set of values used when the setting kind is a choice.
    /// </summary>
    public string[] AllowedValues { get; }
}
