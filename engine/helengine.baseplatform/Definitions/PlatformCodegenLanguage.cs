namespace helengine.baseplatform.Definitions;

/// <summary>
/// Identifies the language emitted by the generated-core codegen pipeline.
/// </summary>
public enum PlatformCodegenLanguage {
    /// <summary>
    /// The generated-core pipeline emits C++ source.
    /// </summary>
    Cpp = 0,

    /// <summary>
    /// The generated-core pipeline emits C source.
    /// </summary>
    C = 1
}
