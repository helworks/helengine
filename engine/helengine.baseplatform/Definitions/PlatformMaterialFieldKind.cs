namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes the kind of editor control one material field requires.
/// </summary>
public enum PlatformMaterialFieldKind {
    /// <summary>
    /// Indicates the editor should collect a boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Indicates the editor should collect free-form text.
    /// </summary>
    Text,

    /// <summary>
    /// Indicates the editor should collect one value from a closed set of options.
    /// </summary>
    Choice,

    /// <summary>
    /// Indicates the editor should collect a numeric value.
    /// </summary>
    Number,

    /// <summary>
    /// Indicates the editor should collect an asset reference.
    /// </summary>
    AssetReference,

    /// <summary>
    /// Indicates the editor should collect a color value.
    /// </summary>
    Color
}
