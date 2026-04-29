namespace helengine.platforms;

/// <summary>
/// Loads available platforms from one explicitly configured development shared toolchain root.
/// </summary>
public sealed class DevelopmentPlatformProvider : IAvailablePlatformProvider {
    /// <summary>
    /// Stores the development shared toolchain root override used by source or debug builds.
    /// </summary>
    string DevelopmentSharedToolchainRootPath { get; }

    /// <summary>
    /// Initializes one development platform provider.
    /// </summary>
    /// <param name="options">Platform-discovery options containing the development override path.</param>
    public DevelopmentPlatformProvider(PlatformDiscoveryOptions options) {
        DevelopmentSharedToolchainRootPath = options.DevelopmentSharedToolchainRootPath;
    }

    /// <summary>
    /// Attempts to load the available platforms for the supplied engine version from the configured development override root.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <param name="platforms">Resolved platforms when the development override is configured.</param>
    /// <returns><c>true</c> when the development override is configured; otherwise <c>false</c>.</returns>
    public bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms) {
        platforms = Array.Empty<AvailablePlatformDescriptor>();

        if (string.IsNullOrWhiteSpace(DevelopmentSharedToolchainRootPath)) {
            return false;
        }

        InstalledPlatformProvider provider = new InstalledPlatformProvider(DevelopmentSharedToolchainRootPath);
        return provider.TryLoadPlatforms(engineVersion, out platforms);
    }
}
