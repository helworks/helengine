namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes which side of the build graph owns the final cook for one asset kind.
/// </summary>
public enum PlatformAssetCookOwnershipKind {
    /// <summary>
    /// Indicates the shared editor build graph emits the final runtime artifact directly.
    /// </summary>
    EditorOwned = 0,

    /// <summary>
    /// Indicates the platform builder must produce the final runtime artifact from editor-selected source inputs.
    /// </summary>
    BuilderOwned = 1
}
