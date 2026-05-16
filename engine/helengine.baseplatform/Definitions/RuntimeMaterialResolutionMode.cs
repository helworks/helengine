namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes how generated player runtimes resolve packaged material assets.
/// </summary>
public enum RuntimeMaterialResolutionMode {
    /// <summary>
    /// Resolves authored material assets together with one shader package and builds runtime materials from raw data.
    /// </summary>
    RawShaderBacked = 0,

    /// <summary>
    /// Resolves one platform-owned cooked material asset and asks the runtime renderer to materialize it directly.
    /// </summary>
    CookedPlatformOwned = 1
}
