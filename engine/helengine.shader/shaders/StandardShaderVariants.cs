namespace helengine;

/// <summary>
/// Provides the complete ordered Standard Shader variant contract required by every shader-capable target.
/// </summary>
public static class StandardShaderVariants {
    /// <summary>
    /// Stores the immutable ordered Standard Shader variant definitions.
    /// </summary>
    static readonly List<StandardShaderVariant> AllValue = [
        new StandardShaderVariant("ForwardStandard", "VS", "PS", []),
        new StandardShaderVariant("ForwardStandardShadowed", "VS", "PS", ["HELENGINE_STANDARD_SHADOWED=1"]),
        new StandardShaderVariant("ShadowDepth", "VS", "ShadowDepthPS", ["HELENGINE_STANDARD_SHADOW_DEPTH=1"])
    ];

    /// <summary>
    /// Gets every Standard Shader variant in deterministic compile and bundle order.
    /// </summary>
    public static IReadOnlyList<StandardShaderVariant> All => AllValue;
}
