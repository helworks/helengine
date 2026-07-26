namespace helengine.baseplatform.Results;

/// <summary>
/// Describes one shader asset and optional material-selected program pair required by a platform shader staging capability.
/// </summary>
public sealed class PlatformShaderDependency {
    /// <summary>
    /// Stores the stable shader asset identifier persisted by materials.
    /// </summary>
    readonly string ShaderAssetIdValue;

    /// <summary>
    /// Stores the selected vertex-program name when the material chooses one explicit program pair.
    /// </summary>
    readonly string VertexProgramNameValue;

    /// <summary>
    /// Stores the selected pixel-program name when the material chooses one explicit program pair.
    /// </summary>
    readonly string PixelProgramNameValue;

    /// <summary>
    /// Stores the selected shader variant when the material chooses one explicit program pair.
    /// </summary>
    readonly string VariantNameValue;

    /// <summary>
    /// Initializes one material-reported shader dependency.
    /// </summary>
    /// <param name="shaderAssetId">Stable shader asset identifier persisted by the material.</param>
    /// <param name="vertexProgramName">Selected vertex-program name, or empty only when no platform shader staging is required.</param>
    /// <param name="pixelProgramName">Selected pixel-program name, or empty only when no platform shader staging is required.</param>
    /// <param name="variantName">Selected shader variant, or empty only when no platform shader staging is required.</param>
    public PlatformShaderDependency(string shaderAssetId, string vertexProgramName, string pixelProgramName, string variantName) {
        if (string.IsNullOrWhiteSpace(shaderAssetId)) {
            throw new ArgumentException("Shader asset id is required.", nameof(shaderAssetId));
        }

        bool hasVertexProgram = !string.IsNullOrWhiteSpace(vertexProgramName);
        bool hasPixelProgram = !string.IsNullOrWhiteSpace(pixelProgramName);
        bool hasVariant = !string.IsNullOrWhiteSpace(variantName);
        if (hasVertexProgram != hasPixelProgram || hasVertexProgram != hasVariant) {
            throw new ArgumentException("Shader dependencies must provide either a complete program pair and variant or none of them.", nameof(vertexProgramName));
        }

        ShaderAssetIdValue = shaderAssetId;
        VertexProgramNameValue = vertexProgramName ?? string.Empty;
        PixelProgramNameValue = pixelProgramName ?? string.Empty;
        VariantNameValue = variantName ?? string.Empty;
    }

    /// <summary>
    /// Gets the stable shader asset identifier persisted by materials.
    /// </summary>
    public string ShaderAssetId => ShaderAssetIdValue;

    /// <summary>
    /// Gets the selected vertex-program name, if the dependency owns an explicit program pair.
    /// </summary>
    public string VertexProgramName => VertexProgramNameValue;

    /// <summary>
    /// Gets the selected pixel-program name, if the dependency owns an explicit program pair.
    /// </summary>
    public string PixelProgramName => PixelProgramNameValue;

    /// <summary>
    /// Gets the selected shader variant, if the dependency owns an explicit program pair.
    /// </summary>
    public string VariantName => VariantNameValue;

    /// <summary>
    /// Gets whether the dependency includes a complete material-selected program-pair lookup key.
    /// </summary>
    public bool HasProgramPair => !string.IsNullOrWhiteSpace(VertexProgramNameValue);
}
