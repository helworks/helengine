namespace helengine;

/// <summary>
/// Defines one platform-independent Standard Shader program pair and the compile defines that select its behavior.
/// </summary>
public sealed class StandardShaderVariant {
    /// <summary>
    /// Stores the immutable source define names used by this variant.
    /// </summary>
    readonly List<string> DefinesValue;

    /// <summary>
    /// Initializes one complete Standard Shader variant definition.
    /// </summary>
    /// <param name="name">Stable variant name used by compiled shader assets and target artifact bundles.</param>
    /// <param name="vertexEntryPoint">Vertex entry point compiled for the variant.</param>
    /// <param name="pixelEntryPoint">Pixel entry point compiled for the variant.</param>
    /// <param name="defines">Source defines required by the variant.</param>
    public StandardShaderVariant(string name, string vertexEntryPoint, string pixelEntryPoint, List<string> defines) {
        Name = RequireText(name, nameof(name));
        VertexEntryPoint = RequireText(vertexEntryPoint, nameof(vertexEntryPoint));
        PixelEntryPoint = RequireText(pixelEntryPoint, nameof(pixelEntryPoint));
        DefinesValue = CopyDefines(defines);
    }

    /// <summary>
    /// Gets the stable variant name shared by all shader-capable targets.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the vertex stage entry point required by this variant.
    /// </summary>
    public string VertexEntryPoint { get; }

    /// <summary>
    /// Gets the pixel stage entry point required by this variant.
    /// </summary>
    public string PixelEntryPoint { get; }

    /// <summary>
    /// Gets the immutable compile define names required by this variant.
    /// </summary>
    public IReadOnlyList<string> Defines => DefinesValue;

    /// <summary>
    /// Validates one required textual variant value.
    /// </summary>
    /// <param name="value">Candidate value to validate.</param>
    /// <param name="parameterName">Parameter name used in the diagnostic.</param>
    /// <returns>The validated text.</returns>
    static string RequireText(string value, string parameterName) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("Standard Shader variant text cannot be blank.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// Copies and validates the source defines required by one variant.
    /// </summary>
    /// <param name="defines">Candidate define list.</param>
    /// <returns>Immutable copied define list.</returns>
    static List<string> CopyDefines(List<string> defines) {
        if (defines == null) {
            throw new ArgumentNullException(nameof(defines));
        }

        List<string> copiedDefines = new();
        for (int index = 0; index < defines.Count; index++) {
            copiedDefines.Add(RequireText(defines[index], nameof(defines)));
        }

        return copiedDefines;
    }
}
