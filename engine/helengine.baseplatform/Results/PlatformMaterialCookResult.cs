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
    /// Initializes one material cook result.
    /// </summary>
    /// <param name="cookedMaterialBytes">Serialized cooked material asset bytes the packager should write into the staged output.</param>
    /// <param name="referencedShaderAssetIds">Deduplicated shader asset ids referenced by the cooked material payload.</param>
    public PlatformMaterialCookResult(byte[] cookedMaterialBytes, string[] referencedShaderAssetIds)
        : this(cookedMaterialBytes, CreateIdOnlyDependencies(referencedShaderAssetIds)) {
    }

    /// <summary>
    /// Initializes one material cook result with explicit shader dependencies that preserve material-selected program-pair lookup keys.
    /// </summary>
    /// <param name="cookedMaterialBytes">Serialized cooked material asset bytes the packager should write into the staged output.</param>
    /// <param name="referencedShaderDependencies">Shader dependencies referenced by the cooked material payload.</param>
    PlatformMaterialCookResult(byte[] cookedMaterialBytes, PlatformShaderDependency[] referencedShaderDependencies) {
        if (cookedMaterialBytes == null) {
            throw new ArgumentNullException(nameof(cookedMaterialBytes), "Cooked material bytes are required.");
        } else if (referencedShaderDependencies == null) {
            throw new ArgumentNullException(nameof(referencedShaderDependencies), "Referenced shader dependencies are required.");
        }

        string[] shaderAssetIds = new string[referencedShaderDependencies.Length];
        for (int index = 0; index < referencedShaderDependencies.Length; index++) {
            PlatformShaderDependency dependency = referencedShaderDependencies[index] ?? throw new ArgumentException("Referenced shader dependencies cannot contain null entries.", nameof(referencedShaderDependencies));
            shaderAssetIds[index] = dependency.ShaderAssetId;
        }

        CookedMaterialBytes = [.. cookedMaterialBytes];
        ReferencedShaderDependenciesValue = [.. referencedShaderDependencies];
        ReferencedShaderAssetIds = shaderAssetIds;
    }

    /// <summary>
    /// Creates one material cook result with complete shader dependencies while avoiding ambiguity with empty legacy shader-id arrays.
    /// </summary>
    /// <param name="cookedMaterialBytes">Serialized cooked material asset bytes the packager should write into the staged output.</param>
    /// <param name="referencedShaderDependencies">Shader dependencies referenced by the cooked material payload.</param>
    /// <returns>Material cook result preserving complete shader lookup keys.</returns>
    public static PlatformMaterialCookResult CreateWithDependencies(byte[] cookedMaterialBytes, PlatformShaderDependency[] referencedShaderDependencies) {
        return new PlatformMaterialCookResult(cookedMaterialBytes, referencedShaderDependencies);
    }

    /// <summary>
    /// Gets the serialized cooked material asset bytes the packager should write into the staged output.
    /// </summary>
    public byte[] CookedMaterialBytes { get; }

    /// <summary>
    /// Gets the deduplicated shader asset ids referenced by the cooked material payload.
    /// </summary>
    public string[] ReferencedShaderAssetIds { get; }

    /// <summary>
    /// Gets copied shader dependencies including their material-selected program-pair lookup keys when the builder provides them.
    /// </summary>
    public PlatformShaderDependency[] ReferencedShaderDependencies => [.. ReferencedShaderDependenciesValue];

    /// <summary>
    /// Converts legacy shader asset identifiers into dependencies without selected program-pair keys.
    /// </summary>
    /// <param name="referencedShaderAssetIds">Legacy shader asset identifiers.</param>
    /// <returns>Equivalent id-only dependencies.</returns>
    static PlatformShaderDependency[] CreateIdOnlyDependencies(string[] referencedShaderAssetIds) {
        if (referencedShaderAssetIds == null) {
            throw new ArgumentNullException(nameof(referencedShaderAssetIds), "Referenced shader asset ids are required.");
        }

        PlatformShaderDependency[] dependencies = new PlatformShaderDependency[referencedShaderAssetIds.Length];
        for (int index = 0; index < dependencies.Length; index++) {
            dependencies[index] = new PlatformShaderDependency(referencedShaderAssetIds[index], string.Empty, string.Empty, string.Empty);
        }

        return dependencies;
    }
}
