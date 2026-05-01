namespace helengine.platforms;

/// <summary>
/// Stores the platform entries loaded from one engine-level catalog file.
/// </summary>
public sealed class PlatformInstallationManifest {
    /// <summary>
    /// Initializes one platform installation manifest.
    /// </summary>
    /// <param name="platforms">Platform entries contained in the manifest.</param>
    public PlatformInstallationManifest(IReadOnlyList<PlatformInstallationEntry> platforms) {
        Platforms = platforms;
    }

    /// <summary>
    /// Gets the platform entries contained in the manifest.
    /// </summary>
    public IReadOnlyList<PlatformInstallationEntry> Platforms { get; }
}
