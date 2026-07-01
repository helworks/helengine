namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one typed platform-specific member exposed for one serialized component type.
/// </summary>
public class PlatformComponentMemberDefinition {
    /// <summary>
    /// Initializes one platform-specific component member definition.
    /// </summary>
    /// <param name="componentTypeId">Stable serialized component type identifier that owns the member.</param>
    /// <param name="memberName">Stable synthetic member name used across editor persistence and runtime packaging.</param>
    /// <param name="displayName">Human-readable label shown by editor property inspectors.</param>
    /// <param name="valueKind">Serialized value shape used by the member.</param>
    /// <param name="defaultValue">Default serialized value emitted when no detached override was authored.</param>
    /// <param name="order">Explicit display order used by editor property inspectors.</param>
    public PlatformComponentMemberDefinition(
        string componentTypeId,
        string memberName,
        string displayName,
        PlatformComponentMemberValueKind valueKind,
        string defaultValue,
        int order = int.MaxValue) {
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id is required.", nameof(componentTypeId));
        }
        if (string.IsNullOrWhiteSpace(memberName)) {
            throw new ArgumentException("Member name is required.", nameof(memberName));
        }
        if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        ComponentTypeId = componentTypeId;
        MemberName = memberName;
        DisplayName = displayName;
        ValueKind = valueKind;
        DefaultValue = defaultValue ?? string.Empty;
        Order = order;
    }

    /// <summary>
    /// Gets the stable serialized component type identifier that owns the member.
    /// </summary>
    public string ComponentTypeId { get; }

    /// <summary>
    /// Gets the stable synthetic member name.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets the human-readable editor label shown for the member.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the serialized value shape used by the member.
    /// </summary>
    public PlatformComponentMemberValueKind ValueKind { get; }

    /// <summary>
    /// Gets the default serialized value used when no detached override was authored.
    /// </summary>
    public string DefaultValue { get; }

    /// <summary>
    /// Gets the explicit display order used by editor property inspectors.
    /// </summary>
    public int Order { get; }
}
