namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one builder-defined field inside a material authoring schema.
/// </summary>
public class PlatformMaterialFieldDefinition {
    /// <summary>
    /// Initializes one material field definition.
    /// </summary>
    /// <param name="fieldId">Stable material field identifier.</param>
    /// <param name="displayName">Human-readable field label shown in the editor.</param>
    /// <param name="fieldKind">The editor control kind used to collect the field value.</param>
    /// <param name="defaultValue">Default serialized field value used when the asset has not stored an override yet.</param>
    /// <param name="required">Whether the field must be provided before the platform can cook the material.</param>
    /// <param name="allowedValues">Closed set of values used when the field kind is a choice.</param>
    public PlatformMaterialFieldDefinition(
        string fieldId,
        string displayName,
        PlatformMaterialFieldKind fieldKind,
        string defaultValue,
        bool required,
        string[] allowedValues) {
        if (string.IsNullOrWhiteSpace(fieldId)) {
            throw new ArgumentException("Material field id is required.", nameof(fieldId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Material field display name is required.", nameof(displayName));
        } else if (defaultValue == null) {
            throw new ArgumentNullException(nameof(defaultValue), "Material field default value is required.");
        } else if (allowedValues == null) {
            throw new ArgumentNullException(nameof(allowedValues), "Material field allowed values are required.");
        } else if (Array.Exists(allowedValues, allowedValue => string.IsNullOrWhiteSpace(allowedValue))) {
            throw new ArgumentException("Material field allowed values cannot contain blank entries.", nameof(allowedValues));
        }

        FieldId = fieldId;
        DisplayName = displayName;
        FieldKind = fieldKind;
        DefaultValue = defaultValue;
        Required = required;
        AllowedValues = [.. allowedValues];
    }

    /// <summary>
    /// Gets the stable material field identifier.
    /// </summary>
    public string FieldId { get; }

    /// <summary>
    /// Gets the human-readable field label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the editor control kind used to collect the field value.
    /// </summary>
    public PlatformMaterialFieldKind FieldKind { get; }

    /// <summary>
    /// Gets the default serialized field value used when the asset has not stored an override yet.
    /// </summary>
    public string DefaultValue { get; }

    /// <summary>
    /// Gets whether the field must be provided before the platform can cook the material.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the closed set of values used when the field kind is a choice.
    /// </summary>
    public string[] AllowedValues { get; }
}
