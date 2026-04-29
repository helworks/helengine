namespace helengine.platforms;

/// <summary>
/// Stores the engine-version-to-platform binding persisted by launcher-managed installs.
/// </summary>
public sealed class InstalledEnginePlatformBinding {
    /// <summary>
    /// Initializes one installed engine-platform binding.
    /// </summary>
    /// <param name="engineVersion">Exact engine version that owns the platform binding.</param>
    /// <param name="platformId">Stable platform identifier bound to the engine version.</param>
    public InstalledEnginePlatformBinding(string engineVersion, string platformId) {
        EngineVersion = engineVersion;
        PlatformId = platformId;
    }

    /// <summary>
    /// Gets the exact engine version that owns the platform binding.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the stable platform identifier bound to the engine version.
    /// </summary>
    public string PlatformId { get; }
}
