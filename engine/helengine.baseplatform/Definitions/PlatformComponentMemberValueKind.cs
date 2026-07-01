namespace helengine.baseplatform.Definitions;

/// <summary>
/// Identifies the serialized value shape used by one platform-specific component member definition.
/// </summary>
public enum PlatformComponentMemberValueKind {
    /// <summary>
    /// Stores one UTF-16 string value.
    /// </summary>
    String,

    /// <summary>
    /// Stores one boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Stores one 32-bit signed integer value.
    /// </summary>
    Int32,

    /// <summary>
    /// Stores one 32-bit floating-point value.
    /// </summary>
    Single
}
