namespace helengine.baseplatform.Manifest;

/// <summary>
/// Defines stable metadata keys stored on resolved platform build scenes.
/// </summary>
public static class PlatformBuildSceneMetadataKeys {
    /// <summary>
    /// Metadata key that stores the cooked runtime-relative scene payload path.
    /// </summary>
    public const string CookedRelativePath = "cooked-relative-path";

    /// <summary>
    /// Metadata key that stores the compact 3D physics scene feature mask required by the cooked scene.
    /// </summary>
    public const string Physics3DSceneFeatureFlags = "physics3d-scene-feature-flags";

    /// <summary>
    /// Metadata key that stores the semicolon-delimited automatic runtime component type ids referenced by the cooked scene.
    /// </summary>
    public const string AutomaticRuntimeComponentTypeIds = "automatic-runtime-component-type-ids";
}
