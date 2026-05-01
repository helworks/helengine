namespace helengine.platforms;

/// <summary>
/// Loads available platforms from one explicitly configured engine user-settings root.
/// </summary>
public sealed class DevelopmentPlatformProvider : IAvailablePlatformProvider {
    /// <summary>
    /// Stores the engine user-settings root override used by source or debug builds.
    /// </summary>
    string EngineUserSettingsRootPath { get; }

    /// <summary>
    /// Initializes one development platform provider.
    /// </summary>
    /// <param name="options">Platform-discovery options containing the engine user-settings override path.</param>
    public DevelopmentPlatformProvider(PlatformDiscoveryOptions options) {
        EngineUserSettingsRootPath = options.EngineUserSettingsRootPath;
    }

    /// <summary>
    /// Attempts to load the available platforms for the supplied engine version from the configured engine user-settings root.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <param name="platforms">Resolved platforms when the engine override is configured.</param>
    /// <returns><c>true</c> when the engine override is configured; otherwise <c>false</c>.</returns>
    public bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms) {
        platforms = Array.Empty<AvailablePlatformDescriptor>();

        if (string.IsNullOrWhiteSpace(EngineUserSettingsRootPath)) {
            return false;
        }

        PlatformInstallationResolver installationResolver = new PlatformInstallationResolver(EngineUserSettingsRootPath);
        if (installationResolver.TryLoadPlatforms(engineVersion, out platforms)) {
            return true;
        }

        return false;
    }
}
