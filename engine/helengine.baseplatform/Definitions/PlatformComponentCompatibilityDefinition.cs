namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes how one platform handles one serialized component type.
/// </summary>
public class PlatformComponentCompatibilityDefinition {
    /// <summary>
    /// Initializes one component compatibility definition.
    /// </summary>
    /// <param name="componentTypeId">Stable serialized component type identifier.</param>
    /// <param name="compatibilityKind">How the platform handles the component.</param>
    /// <param name="reason">Human-readable reason or summary.</param>
    /// <param name="remediation">Optional remediation text shown when the component is unsupported or transformed.</param>
    public PlatformComponentCompatibilityDefinition(
        string componentTypeId,
        PlatformComponentCompatibilityKind compatibilityKind,
        string reason,
        string remediation) {
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id is required.", nameof(componentTypeId));
        }

        ComponentTypeId = componentTypeId;
        CompatibilityKind = compatibilityKind;
        Reason = reason ?? string.Empty;
        Remediation = remediation ?? string.Empty;
    }

    /// <summary>
    /// Gets the stable serialized component type identifier.
    /// </summary>
    public string ComponentTypeId { get; }

    /// <summary>
    /// Gets how the platform handles the component.
    /// </summary>
    public PlatformComponentCompatibilityKind CompatibilityKind { get; }

    /// <summary>
    /// Gets the human-readable reason or summary.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the optional remediation text.
    /// </summary>
    public string Remediation { get; }
}
