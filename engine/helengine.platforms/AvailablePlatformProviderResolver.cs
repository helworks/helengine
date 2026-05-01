namespace helengine.platforms;

/// <summary>
/// Resolves the available platform list for one engine version by applying engine-level, launcher-managed, and built-in fallback sources in order.
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
    /// <returns>Available platforms merged from source overrides, launcher-managed state, and fallback defaults.</returns>
    public IReadOnlyList<AvailablePlatformDescriptor> LoadPlatforms(string engineVersion) {
        List<AvailablePlatformDescriptor> platforms = new();
        Dictionary<string, int> platformIndexes = new(StringComparer.Ordinal);
        bool hadAnySourceState = false;

        DevelopmentPlatformProvider developmentProvider = new DevelopmentPlatformProvider(Options);
        if (developmentProvider.TryLoadPlatforms(engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> developmentPlatforms)) {
            hadAnySourceState = true;
            MergePlatforms(platforms, platformIndexes, developmentPlatforms);
        }

        LauncherInstallRoots launcherInstallRoots = LauncherInstallRootLocator.Load();
        InstalledPlatformProvider installedPlatformProvider = new InstalledPlatformProvider(launcherInstallRoots.SharedToolchainRoot);
        if (installedPlatformProvider.TryLoadPlatforms(engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> installedPlatforms)) {
            hadAnySourceState = true;
            MergePlatforms(platforms, platformIndexes, installedPlatforms);
        }

        if (platforms.Count > 0) {
            return platforms;
        }

        if (hadAnySourceState) {
            return Array.Empty<AvailablePlatformDescriptor>();
        }

        return CreateBuiltInFallbackPlatforms();
    }

    /// <summary>
    /// Creates the built-in source-build fallback list used when no persisted platform state is available.
    /// </summary>
    /// <returns>Built-in source-build fallback platforms.</returns>
    static IReadOnlyList<AvailablePlatformDescriptor> CreateBuiltInFallbackPlatforms() {
        return new[] {
            new AvailablePlatformDescriptor("windows", "windows", string.Empty, string.Empty, true)
        };
    }

    /// <summary>
    /// Adds or upgrades platform descriptors in the supplied collection while preserving first-seen order.
    /// </summary>
    /// <param name="platforms">Collection that accumulates merged platform descriptors.</param>
    /// <param name="platformIndexes">Indexes of platform ids already added to the collection.</param>
    /// <param name="sourcePlatforms">Source platforms to merge into the collection.</param>
    static void MergePlatforms(
        List<AvailablePlatformDescriptor> platforms,
        Dictionary<string, int> platformIndexes,
        IReadOnlyList<AvailablePlatformDescriptor> sourcePlatforms) {
        for (int index = 0; index < sourcePlatforms.Count; index++) {
            AvailablePlatformDescriptor platform = sourcePlatforms[index];
            if (!platformIndexes.TryGetValue(platform.Id, out int existingIndex)) {
                platformIndexes.Add(platform.Id, platforms.Count);
                platforms.Add(platform);
                continue;
            }

            AvailablePlatformDescriptor existingPlatform = platforms[existingIndex];
            if (existingPlatform.IsInstalled || !platform.IsInstalled) {
                continue;
            }

            platforms[existingIndex] = platform;
        }
    }
}
