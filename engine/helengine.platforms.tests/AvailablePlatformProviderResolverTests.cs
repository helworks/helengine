using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies platform discovery prefers development overrides, reuses launcher-managed bindings, and falls back only when no persisted state exists.
/// </summary>
public sealed class AvailablePlatformProviderResolverTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory for the current resolver test instance.
    /// </summary>
    public AvailablePlatformProviderResolverTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-platforms-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the isolated temporary directory after the current resolver test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures launcher-managed bindings return the platform ids for the requested engine version only.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenLauncherBindingsMatchEngineVersion_ReturnsMatchingPlatforms() {
        string launcherToolchainRootPath = CreateSharedToolchainRoot(
            "launcher-toolchains",
            """
            {
              "bindings": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "windows"
                },
                {
                  "engineVersion": "1.0.0",
                  "platformId": "linux"
                },
                {
                  "engineVersion": "2.0.0",
                  "platformId": "android"
                }
              ]
            }
            """);
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Collection(
            platforms,
            platform => Assert.Equal("windows", platform.Id),
            platform => Assert.Equal("linux", platform.Id));
    }

    /// <summary>
    /// Ensures one configured development override is used before launcher-managed registry state.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenDevelopmentOverrideIsConfigured_PrefersDevelopmentBindingsOverLauncherBindings() {
        string developmentToolchainRootPath = CreateSharedToolchainRoot(
            "development-toolchains",
            """
            {
              "bindings": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "windows"
                },
                {
                  "engineVersion": "1.0.0",
                  "platformId": "steamdeck"
                }
              ]
            }
            """);
        string launcherToolchainRootPath = CreateSharedToolchainRoot(
            "launcher-toolchains",
            """
            {
              "bindings": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "android"
                }
              ]
            }
            """);
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(developmentToolchainRootPath),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Collection(
            platforms,
            platform => Assert.Equal("windows", platform.Id),
            platform => Assert.Equal("steamdeck", platform.Id));
    }

    /// <summary>
    /// Ensures missing launcher state falls back to the built-in source-build Windows platform.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenLauncherStateIsMissing_ReturnsBuiltInWindowsFallback() {
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(),
            new TestLauncherInstallRootLocator(string.Empty, Path.Combine(TempDirectoryPath, "missing-toolchains")));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Single(platforms);
        Assert.Equal("windows", platforms[0].Id);
    }

    /// <summary>
    /// Ensures one engine version with no matching bindings returns an empty list instead of leaking bindings for other engines.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenEngineVersionHasNoMatchingBindings_ReturnsEmptyList() {
        string launcherToolchainRootPath = CreateSharedToolchainRoot(
            "launcher-toolchains",
            """
            {
              "bindings": [
                {
                  "engineVersion": "2.0.0",
                  "platformId": "android"
                }
              ]
            }
            """);
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Empty(platforms);
    }

    /// <summary>
    /// Creates one temporary shared toolchain root containing the supplied installed-binding manifest JSON.
    /// </summary>
    /// <param name="directoryName">Directory name used under the test root.</param>
    /// <param name="manifestJson">Installed-binding manifest JSON to write.</param>
    /// <returns>Absolute path to the created shared toolchain root.</returns>
    string CreateSharedToolchainRoot(string directoryName, string manifestJson) {
        string sharedToolchainRootPath = Path.Combine(TempDirectoryPath, directoryName);
        Directory.CreateDirectory(sharedToolchainRootPath);
        File.WriteAllText(Path.Combine(sharedToolchainRootPath, "installed-bindings.json"), manifestJson);
        return sharedToolchainRootPath;
    }
}
