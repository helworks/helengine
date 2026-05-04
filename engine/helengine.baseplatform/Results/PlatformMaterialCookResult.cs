namespace helengine.baseplatform.Results;

/// <summary>
/// Captures one builder-owned cooked material payload plus referenced shader dependencies.
/// </summary>
public sealed class PlatformMaterialCookResult {
    /// <summary>
    /// Initializes one material cook result.
    /// </summary>
    /// <param name="cookedMaterialBytes">Serialized cooked material asset bytes the packager should write into the staged output.</param>
    /// <param name="referencedShaderAssetIds">Deduplicated shader asset ids referenced by the cooked material payload.</param>
    public PlatformMaterialCookResult(byte[] cookedMaterialBytes, string[] referencedShaderAssetIds) {
        if (cookedMaterialBytes == null) {
            throw new ArgumentNullException(nameof(cookedMaterialBytes), "Cooked material bytes are required.");
        } else if (referencedShaderAssetIds == null) {
            throw new ArgumentNullException(nameof(referencedShaderAssetIds), "Referenced shader asset ids are required.");
        } else if (Array.Exists(referencedShaderAssetIds, shaderAssetId => string.IsNullOrWhiteSpace(shaderAssetId))) {
            throw new ArgumentException("Referenced shader asset ids cannot contain blank entries.", nameof(referencedShaderAssetIds));
        }

        CookedMaterialBytes = [.. cookedMaterialBytes];
        ReferencedShaderAssetIds = [.. referencedShaderAssetIds];
    }

    /// <summary>
    /// Gets the serialized cooked material asset bytes the packager should write into the staged output.
    /// </summary>
    public byte[] CookedMaterialBytes { get; }

    /// <summary>
    /// Gets the deduplicated shader asset ids referenced by the cooked material payload.
    /// </summary>
    public string[] ReferencedShaderAssetIds { get; }
}
