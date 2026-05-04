namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes the physical media layout strategy a platform package uses.
/// </summary>
public enum PlatformMediaLayoutKind {
    /// <summary>
    /// Writes a normal install-tree style output.
    /// </summary>
    InstallTree = 0,

    /// <summary>
    /// Writes a disc-image oriented output.
    /// </summary>
    DiscImage = 1
}
