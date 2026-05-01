using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies platform discovery prefers engine-level manifests, preserves missing entries, and upgrades them when installed payloads exist.
/// </summary>
public sealed class AvailablePlatformProviderResolverTests : IDisposable {
    /// <summary>
    /// Temporary root used for the resolver tests.
    /// </summary>
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory for the current resolver test instance.
    /// </summary>
    public AvailablePlatformProviderResolverTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "codex-helengine-platforms-tests", Guid.NewGuid().ToString("N"));
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
    /// Ensures the engine manifest returns every known platform for the requested engine version and flags missing payloads.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenEngineManifestContainsInstalledAndMissingEntries_ReturnsAllKnownPlatforms() {
        string engineUserSettingsRootPath = CreateManifestRoot(
            "engine-user-settings",
            """
            {
              "platforms": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "windows",
                  "displayName": "Windows DirectX",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "platforms/windows"
                },
                {
                  "engineVersion": "1.0.0",
                  "platformId": "linux",
                  "displayName": "Linux Vulkan",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "platforms/linux"
                },
                {
                  "engineVersion": "2.0.0",
                  "platformId": "android",
                  "displayName": "Android Vulkan",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "platforms/android"
                }
              ]
            }
            """);

        Directory.CreateDirectory(Path.Combine(engineUserSettingsRootPath, "platforms", "windows"));

        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(engineUserSettingsRootPath),
            new TestLauncherInstallRootLocator(string.Empty, string.Empty));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Collection(
            platforms,
            platform => {
                Assert.Equal("windows", platform.Id);
                Assert.True(platform.IsInstalled);
                Assert.Equal(Path.GetFullPath(Path.Combine(engineUserSettingsRootPath, "platforms/windows")), platform.PlayerSourceRootPath);
            },
            platform => {
                Assert.Equal("linux", platform.Id);
                Assert.False(platform.IsInstalled);
                Assert.Equal(Path.GetFullPath(Path.Combine(engineUserSettingsRootPath, "platforms/linux")), platform.PlayerSourceRootPath);
            });
    }

    /// <summary>
    /// Ensures one configured launcher catalog upgrades a missing development entry when the payload is installed there.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenLauncherPayloadExists_UpgradesMissingDevelopmentEntry() {
        string engineUserSettingsRootPath = CreateManifestRoot(
            "engine-user-settings",
            """
            {
              "platforms": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "windows",
                  "displayName": "Windows DirectX",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "platforms/windows"
                }
              ]
            }
            """);

        string launcherToolchainRootPath = CreateManifestRoot(
            "launcher-toolchains",
            """
            {
              "platforms": [
                {
                  "engineVersion": "1.0.0",
                  "platformId": "windows",
                  "displayName": "Windows DirectX",
                  "builderAssemblyPath": "builders/windows/helengine.windows.builder.dll",
                  "playerSourceRootPath": "players/windows"
                }
              ]
            }
            """);

        Directory.CreateDirectory(Path.Combine(launcherToolchainRootPath, "builders", "windows"));
        File.WriteAllText(Path.Combine(launcherToolchainRootPath, "builders", "windows", "helengine.windows.builder.dll"), string.Empty);
        Directory.CreateDirectory(Path.Combine(launcherToolchainRootPath, "players", "windows"));

        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(engineUserSettingsRootPath),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Single(platforms);
        Assert.Equal("windows", platforms[0].Id);
        Assert.True(platforms[0].IsInstalled);
        Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "players/windows")), platforms[0].PlayerSourceRootPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "builders/windows/helengine.windows.builder.dll")), platforms[0].BuilderAssemblyPath);
    }

    /// <summary>
    /// Ensures one engine version with no matching entries returns an empty list instead of leaking entries for other engines.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenEngineVersionHasNoMatchingEntries_ReturnsEmptyList() {
        string engineUserSettingsRootPath = CreateManifestRoot(
            "engine-user-settings",
            """
            {
              "platforms": [
                {
                  "engineVersion": "2.0.0",
                  "platformId": "android",
                  "displayName": "Android Vulkan",
                  "builderAssemblyPath": "",
                  "playerSourceRootPath": "platforms/android"
                }
              ]
            }
            """);

        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(engineUserSettingsRootPath),
            new TestLauncherInstallRootLocator(string.Empty, string.Empty));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Empty(platforms);
    }

    /// <summary>
    /// Creates one manifest root containing the supplied manifest JSON.
    /// </summary>
    /// <param name="directoryName">Directory name used under the test root.</param>
    /// <param name="manifestJson">Manifest JSON to write.</param>
    /// <returns>Absolute path to the created manifest root.</returns>
    string CreateManifestRoot(string directoryName, string manifestJson) {
        string manifestRootPath = Path.Combine(TempDirectoryPath, directoryName);
        Directory.CreateDirectory(manifestRootPath);
        File.WriteAllText(Path.Combine(manifestRootPath, "platforms.json"), manifestJson);
        return manifestRootPath;
    }
}
