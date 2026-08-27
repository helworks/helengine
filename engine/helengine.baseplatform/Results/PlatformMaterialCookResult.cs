namespace helengine.baseplatform.Results;

/// <summary>
/// Captures one builder-owned cooked material payload plus referenced shader dependencies.
/// </summary>
public sealed class PlatformMaterialCookResult {
    /// <summary>
    /// Stores material-selected shader dependencies including optional program-pair lookup keys.
    /// </summary>
    readonly PlatformShaderDependency[] ReferencedShaderDependenciesValue;

    /// <summary>
    /// Initializes one material cook result with explicit shader dependencies that preserve material-selected program-pair lookup keys.
    /// </summary>
    /// <param name="cookedMaterialBytes">Serialized cooked material asset bytes the packager should write into the staged output.</param>
    /// <param name="referencedShaderDependencies">Shader dependencies referenced by the cooked material payload.</param>
    public PlatformMaterialCookResult(byte[] cookedMaterialBytes, PlatformShaderDependency[] referencedShaderDependencies) {
        if (cookedMaterialBytes == null) {
            throw new ArgumentNullException(nameof(cookedMaterialBytes), "Cooked material bytes are required.");
        } else if (referencedShaderDependencies == null) {
            throw new ArgumentNullException(nameof(referencedShaderDependencies), "Referenced shader dependencies are required.");
        }

        for (int index = 0; index < referencedShaderDependencies.Length; index++) {
            PlatformShaderDependency dependency = referencedShaderDependencies[index] ?? throw new ArgumentException("Referenced shader dependencies cannot contain null entries.", nameof(referencedShaderDependencies));
        }

        CookedMaterialBytes = [.. cookedMaterialBytes];
        ReferencedShaderDependenciesValue = [.. referencedShaderDependencies];
    }

    /// <summary>
    /// Gets the serialized cooked material asset bytes the packager should write into the staged output.
    /// </summary>
    public byte[] CookedMaterialBytes { get; }

    /// <summary>
    /// Gets copied shader dependencies including their material-selected program-pair lookup keys when the builder provides them.
    /// </summary>
    public PlatformShaderDependency[] ReferencedShaderDependencies => [.. ReferencedShaderDependenciesValue];

}
