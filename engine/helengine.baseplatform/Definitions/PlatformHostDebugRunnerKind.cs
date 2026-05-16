namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes how one platform launches its host-debug runner from the editor build graph.
/// </summary>
public enum PlatformHostDebugRunnerKind {
    /// <summary>
    /// Indicates that the platform does not publish a host-debug runner.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the platform launches a native executable as its host-debug runner.
    /// </summary>
    NativeExecutable = 1
}
