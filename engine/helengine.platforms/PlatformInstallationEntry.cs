namespace helengine.platforms;

/// <summary>
/// Describes one platform catalog entry stored in an engine-level platform manifest.
/// </summary>
public sealed class PlatformInstallationEntry {
    /// <summary>
    /// Initializes one installation entry.
    /// </summary>
    /// <param name="engineVersion">Exact engine version that owns the platform entry.</param>
    /// <param name="platformId">Stable platform identifier written into project files.</param>
    /// <param name="displayName">Readable platform name shown in editor UI.</param>
    /// <param name="builderAssemblyPath">Absolute or relative path to the platform builder assembly.</param>
    /// <param name="playerSourceRootPath">Absolute or relative path to the platform player source root.</param>
    /// <exception cref="ArgumentException">Thrown when a required string value is missing.</exception>
    public PlatformInstallationEntry(
        string engineVersion,
        string platformId,
        string displayName,
        string builderAssemblyPath,
        string playerSourceRootPath) {
        if (string.IsNullOrWhiteSpace(engineVersion)) {
            throw new ArgumentException("Engine version is required.", nameof(engineVersion));
        } else if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Platform display name is required.", nameof(displayName));
        } else if (string.IsNullOrWhiteSpace(playerSourceRootPath)) {
            throw new ArgumentException("Player source root path is required.", nameof(playerSourceRootPath));
        }

        EngineVersion = engineVersion;
        PlatformId = platformId;
        DisplayName = displayName;
        BuilderAssemblyPath = builderAssemblyPath ?? string.Empty;
        PlayerSourceRootPath = playerSourceRootPath;
    }

    /// <summary>
    /// Gets the exact engine version that owns the platform entry.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the stable platform identifier written into project files.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the readable platform name shown in editor UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the path to the platform builder assembly.
    /// </summary>
    public string BuilderAssemblyPath { get; }

    /// <summary>
    /// Gets the path to the platform player source root.
    /// </summary>
    public string PlayerSourceRootPath { get; }
}
