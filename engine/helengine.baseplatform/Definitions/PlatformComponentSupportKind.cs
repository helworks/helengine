namespace helengine.baseplatform.Definitions;

/// <summary>
/// Identifies how one platform handles one serialized component type.
/// </summary>
public enum PlatformComponentSupportKind {
    /// <summary>
    /// The serialized component record can be emitted unchanged.
    /// </summary>
    PassThrough,

    /// <summary>
    /// The serialized component record should be rewritten before packaging.
    /// </summary>
    Transform,

    /// <summary>
    /// The platform cannot package the component.
    /// </summary>
    Unsupported
}
