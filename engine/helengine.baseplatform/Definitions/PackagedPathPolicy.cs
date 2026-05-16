namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes which packaged file path forms generated player runtimes may consume.
/// </summary>
public enum PackagedPathPolicy {
    /// <summary>
    /// Requires packaged scene references to stay relative to the generated content root.
    /// </summary>
    ContentRelativeOnly = 0,

    /// <summary>
    /// Allows packaged scene references to be rooted absolute paths or content-root-relative paths.
    /// </summary>
    RootedOrContentRelative = 1
}
