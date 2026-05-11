namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes how one platform supports one serialized component type.
/// </summary>
public class PlatformComponentSupportRule {
    /// <summary>
    /// Initializes one component support rule.
    /// </summary>
    /// <param name="componentTypeId">Stable serialized component type identifier.</param>
    /// <param name="supportKind">How the platform handles the component.</param>
    /// <param name="reason">Human-readable reason or summary.</param>
    /// <param name="remediation">Optional remediation text shown when the component is unsupported or transformed.</param>
    public PlatformComponentSupportRule(
        string componentTypeId,
        PlatformComponentSupportKind supportKind,
        string reason,
        string remediation) {
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id is required.", nameof(componentTypeId));
        }

        ComponentTypeId = componentTypeId;
        SupportKind = supportKind;
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
    public PlatformComponentSupportKind SupportKind { get; }

    /// <summary>
    /// Gets the human-readable reason or summary.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the optional remediation text.
    /// </summary>
    public string Remediation { get; }
}
