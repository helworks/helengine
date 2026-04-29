namespace helengine.platforms;

/// <summary>
/// Stores optional platform-discovery overrides used by source or debug editor builds.
/// </summary>
public sealed class PlatformDiscoveryOptions {
    /// <summary>
    /// Initializes one platform-discovery options instance.
    /// </summary>
    /// <param name="developmentSharedToolchainRootPath">Optional shared toolchain root that should override launcher-managed discovery.</param>
    public PlatformDiscoveryOptions(string developmentSharedToolchainRootPath = "") {
        DevelopmentSharedToolchainRootPath = developmentSharedToolchainRootPath ?? string.Empty;
    }

    /// <summary>
    /// Gets the optional development shared toolchain root that overrides launcher-managed discovery when configured.
    /// </summary>
    public string DevelopmentSharedToolchainRootPath { get; }
}
