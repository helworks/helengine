namespace helengine.platforms;

/// <summary>
/// Resolves the available platform list for one engine version from shared development overrides.
/// </summary>
public sealed class AvailablePlatformProviderResolver {
    /// <summary>
    /// Stores the platform-discovery options that may configure development overrides.
    /// </summary>
    PlatformDiscoveryOptions Options { get; }

    /// <summary>
    /// Initializes one available-platform resolver.
    /// </summary>
    /// <param name="options">Platform-discovery options that may configure development overrides.</param>
    public AvailablePlatformProviderResolver(PlatformDiscoveryOptions options) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }

        Options = options;
    }

    /// <summary>
    /// Loads the available platforms for one exact engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <returns>Available platforms loaded from shared development overrides, or an empty list when none exist.</returns>
    public IReadOnlyList<AvailablePlatformDescriptor> LoadPlatforms(string engineVersion) {
        List<AvailablePlatformDescriptor> platforms = new();
        Dictionary<string, int> platformIndexes = new(StringComparer.Ordinal);

        DevelopmentPlatformProvider developmentProvider = new DevelopmentPlatformProvider(Options);
        if (developmentProvider.TryLoadPlatforms(engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> developmentPlatforms)) {
            MergePlatforms(platforms, platformIndexes, developmentPlatforms);
        }

        return platforms;
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
