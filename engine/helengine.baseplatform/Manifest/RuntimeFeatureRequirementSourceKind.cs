namespace helengine.baseplatform.Manifest;

/// <summary>
/// Identifies which authored source required one runtime feature to be present in the final build.
/// </summary>
public enum RuntimeFeatureRequirementSourceKind {
    /// <summary>
    /// Indicates the requirement came from one authored scene.
    /// </summary>
    Scene,

    /// <summary>
    /// Indicates the requirement came from one authored material.
    /// </summary>
    Material,

    /// <summary>
    /// Indicates the requirement came from one authored asset outside a scene or material schema.
    /// </summary>
    Asset,

    /// <summary>
    /// Indicates the requirement came from one runtime type discovered in compiled code.
    /// </summary>
    RuntimeType,

    /// <summary>
    /// Indicates the requirement came from one plugin or package-level declaration.
    /// </summary>
    Plugin
}
