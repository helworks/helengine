namespace helengine.baseplatform.Reporting;

/// <summary>
/// Defines the final outcome kind recorded for one built scene or loose asset.
/// </summary>
public enum PlatformBuildItemOutcomeKind {
    /// <summary>
    /// Indicates the item completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Indicates the item failed to build.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates the item was skipped intentionally.
    /// </summary>
    Skipped,
}
