namespace helengine.baseplatform.Definitions;

/// <summary>
/// Identifies the kind of value a platform setting expects the editor to collect and persist.
/// </summary>
public enum PlatformSettingKind {
    /// <summary>
    /// Represents a boolean setting that is edited as an on or off toggle.
    /// </summary>
    Boolean,

    /// <summary>
    /// Represents a free-form text setting.
    /// </summary>
    Text,

    /// <summary>
    /// Represents a setting that should be chosen from a closed set of string values.
    /// </summary>
    Choice
}
