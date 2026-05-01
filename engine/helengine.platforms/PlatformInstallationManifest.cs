namespace helengine.platforms;

/// <summary>
/// Stores the platform descriptor links loaded from one installation catalog file.
/// </summary>
public sealed class PlatformInstallationManifest {
    /// <summary>
    /// Initializes one platform installation manifest.
    /// </summary>
    /// <param name="platforms">Platform descriptor links contained in the manifest.</param>
    public PlatformInstallationManifest(IReadOnlyList<PlatformInstallationEntry> platforms) {
        Platforms = platforms;
    }

    /// <summary>
    /// Gets the platform descriptor links contained in the manifest.
    /// </summary>
    public IReadOnlyList<PlatformInstallationEntry> Platforms { get; }
}
