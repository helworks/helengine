namespace helengine.baseplatform.Definitions;

/// <summary>Controls how a platform names cooked runtime artifacts.</summary>
public enum PlatformAssetNamingPolicy {
    /// <summary>Preserve the authored asset identifier in the cooked path.</summary>
    PreserveAssetId,
    /// <summary>Use the stable lowercase sixteen-digit runtime asset identifier.</summary>
    RuntimeAssetIdHex16
}