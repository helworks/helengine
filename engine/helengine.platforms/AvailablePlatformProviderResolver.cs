namespace helengine.platforms;

/// <summary>
/// Resolves the available platform list for one engine version by applying development, launcher-managed, and built-in fallback sources in order.
/// </summary>
public sealed class AvailablePlatformProviderResolver {
    /// <summary>
    /// Stores the platform-discovery options that may configure development overrides.
    /// </summary>
    PlatformDiscoveryOptions Options { get; }

    /// <summary>
    /// Stores the launcher install-root locator used to discover launcher-managed shared toolchain state.
    /// </summary>
    WindowsLauncherInstallRootLocator LauncherInstallRootLocator { get; }

    /// <summary>
    /// Initializes one available-platform resolver.
    /// </summary>
    /// <param name="options">Platform-discovery options that may configure development overrides.</param>
    /// <param name="launcherInstallRootLocator">Launcher install-root locator used for launcher-managed discovery.</param>
    public AvailablePlatformProviderResolver(PlatformDiscoveryOptions options, WindowsLauncherInstallRootLocator launcherInstallRootLocator) {
        Options = options;
        LauncherInstallRootLocator = launcherInstallRootLocator;
    }

    /// <summary>
    /// Loads the available platforms for one exact engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <returns>Available platforms resolved from the highest-priority source with available state.</returns>
    public IReadOnlyList<AvailablePlatformDescriptor> LoadPlatforms(string engineVersion) {
        DevelopmentPlatformProvider developmentProvider = new DevelopmentPlatformProvider(Options);
        if (developmentProvider.TryLoadPlatforms(engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> developmentPlatforms)) {
            return developmentPlatforms;
        }

        LauncherInstallRoots launcherInstallRoots = LauncherInstallRootLocator.Load();
        InstalledPlatformProvider installedPlatformProvider = new InstalledPlatformProvider(launcherInstallRoots.SharedToolchainRoot);
        if (installedPlatformProvider.TryLoadPlatforms(engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> installedPlatforms)) {
            return installedPlatforms;
        }

        return CreateBuiltInFallbackPlatforms();
    }

    /// <summary>
    /// Creates the built-in source-build fallback list used when no persisted platform state is available.
    /// </summary>
    /// <returns>Built-in source-build fallback platforms.</returns>
    static IReadOnlyList<AvailablePlatformDescriptor> CreateBuiltInFallbackPlatforms() {
        return new[] {
            new AvailablePlatformDescriptor("windows", "windows")
        };
    }
}
