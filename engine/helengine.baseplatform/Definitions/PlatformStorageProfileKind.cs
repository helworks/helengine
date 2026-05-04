namespace helengine.baseplatform.Definitions;

/// <summary>
/// Identifies the runtime storage model used by a platform build.
/// </summary>
public enum PlatformStorageProfileKind {
    /// <summary>
    /// Runtime content is read from loose files on disk.
    /// </summary>
    LooseFiles = 0,

    /// <summary>
    /// Runtime content is packed into a single container.
    /// </summary>
    SinglePackfile = 1,

    /// <summary>
    /// Runtime content is packed into multiple segmented containers.
    /// </summary>
    SegmentedPackfiles = 2,

    /// <summary>
    /// Runtime content is laid out for disc-oriented streaming.
    /// </summary>
    DiscLayout = 3
}
