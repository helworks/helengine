using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies platform discovery prefers development overrides, reuses launcher-managed installation manifests, and falls back only when no persisted state exists.
/// </summary>
public sealed class AvailablePlatformProviderResolverTests : IDisposable {
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
    /// Ensures the installation manifest returns the platform descriptors for the requested engine version only.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenInstallationManifestLinksPlatformDescriptors_ReturnsMatchingPlatforms() {
        string launcherToolchainRootPath = CreateSharedToolchainRoot(
            "launcher-toolchains",
            "platforms.json",
            """
            {
              "platforms": [
                {
                  "platformDescriptorPath": "windows/platform.json"
                },
                {
                  "platformDescriptorPath": "linux/platform.json"
                },
                {
                  "platformDescriptorPath": "android/platform.json"
                }
              ]
            }
            """);
        WritePlatformDescriptor(launcherToolchainRootPath, "windows/platform.json", "1.0.0", "windows", "Windows DirectX", "builders/windows/helengine.windows.builder.dll", "players/windows");
        WritePlatformDescriptor(launcherToolchainRootPath, "linux/platform.json", "1.0.0", "linux", "Linux Vulkan", "builders/linux/helengine.linux.builder.dll", "players/linux");
        WritePlatformDescriptor(launcherToolchainRootPath, "android/platform.json", "2.0.0", "android", "Android", "builders/android/helengine.android.builder.dll", "players/android");
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Collection(
            platforms,
            platform => {
                Assert.Equal("windows", platform.Id);
                Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "windows/builders/windows/helengine.windows.builder.dll")), platform.BuilderAssemblyPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "windows/players/windows")), platform.PlayerSourceRootPath);
            },
            platform => {
                Assert.Equal("linux", platform.Id);
                Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "linux/builders/linux/helengine.linux.builder.dll")), platform.BuilderAssemblyPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(launcherToolchainRootPath, "linux/players/linux")), platform.PlayerSourceRootPath);
            });
    }

    /// <summary>
    /// Ensures one configured development override is merged with launcher-managed installation state without losing installed platforms.
    /// </summary>
    [Fact]
    public void LoadPlatforms_WhenDevelopmentOverrideIsConfigured_MergesDevelopmentAndLauncherBindings() {
        string developmentToolchainRootPath = CreateSharedToolchainRoot(
            "development-toolchains",
            "platforms.json",
            """
            {
              "platforms": [
                {
                  "platformDescriptorPath": "windows/platform.json"
                },
                {
                  "platformDescriptorPath": "steamdeck/platform.json"
                }
              ]
            }
            """);
        WritePlatformDescriptor(developmentToolchainRootPath, "windows/platform.json", "1.0.0", "windows", "Windows DirectX", "builders/windows/helengine.windows.builder.dll", "players/windows");
        WritePlatformDescriptor(developmentToolchainRootPath, "steamdeck/platform.json", "1.0.0", "steamdeck", "Steam Deck Vulkan", "builders/steamdeck/helengine.steamdeck.builder.dll", "players/steamdeck");
        string launcherToolchainRootPath = CreateSharedToolchainRoot(
            "launcher-toolchains",
            "platforms.json",
            """
            {
              "platforms": [
                {
                  "platformDescriptorPath": "android/platform.json"
                }
              ]
            }
            """);
        WritePlatformDescriptor(launcherToolchainRootPath, "android/platform.json", "1.0.0", "android", "Android Vulkan", "builders/android/helengine.android.builder.dll", "players/android");
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(developmentToolchainRootPath),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Collection(
            platforms,
            platform => {
                Assert.Equal("windows", platform.Id);
                Assert.Equal(Path.GetFullPath(Path.Combine(developmentToolchainRootPath, "windows/builders/windows/helengine.windows.builder.dll")), platform.BuilderAssemblyPath);
                Assert.Equal(Path.GetFullPath(Path.Combine(developmentToolchainRootPath, "windows/players/windows")), platform.PlayerSourceRootPath);
            },
            platform => Assert.Equal("steamdeck", platform.Id),
            platform => Assert.Equal("android", platform.Id));
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
            "platforms.json",
            """
            {
              "platforms": [
                {
                  "platformDescriptorPath": "android/platform.json"
                }
              ]
            }
            """);
        WritePlatformDescriptor(launcherToolchainRootPath, "android/platform.json", "2.0.0", "android", "Android Vulkan", "builders/android/helengine.android.builder.dll", "players/android");
        AvailablePlatformProviderResolver resolver = new AvailablePlatformProviderResolver(
            new PlatformDiscoveryOptions(),
            new TestLauncherInstallRootLocator(string.Empty, launcherToolchainRootPath));

        IReadOnlyList<AvailablePlatformDescriptor> platforms = resolver.LoadPlatforms("1.0.0");

        Assert.Empty(platforms);
    }

    /// <summary>
    /// Writes one per-platform descriptor file inside the supplied test root.
    /// </summary>
    /// <param name="sharedToolchainRootPath">Shared toolchain root that owns the descriptor file.</param>
    /// <param name="descriptorRelativePath">Descriptor path relative to the shared root.</param>
    /// <param name="engineVersion">Exact engine version that owns the descriptor.</param>
    /// <param name="platformId">Stable platform identifier written into project files.</param>
    /// <param name="displayName">Readable platform name shown in editor UI.</param>
    /// <param name="builderAssemblyPath">Builder assembly path stored in the descriptor.</param>
    /// <param name="playerSourceRootPath">Player source root path stored in the descriptor.</param>
    void WritePlatformDescriptor(
        string sharedToolchainRootPath,
        string descriptorRelativePath,
        string engineVersion,
        string platformId,
        string displayName,
        string builderAssemblyPath,
        string playerSourceRootPath) {
        string descriptorFilePath = Path.Combine(sharedToolchainRootPath, descriptorRelativePath);
        string descriptorDirectoryPath = Path.GetDirectoryName(descriptorFilePath) ?? string.Empty;
        Directory.CreateDirectory(descriptorDirectoryPath);
        File.WriteAllText(descriptorFilePath, $$"""
        {
          "engineVersion": "{{engineVersion}}",
          "platformId": "{{platformId}}",
          "displayName": "{{displayName}}",
          "builderAssemblyPath": "{{builderAssemblyPath}}",
          "playerSourceRootPath": "{{playerSourceRootPath}}"
        }
        """);
    }

    /// <summary>
    /// Creates one temporary shared toolchain root containing the supplied manifest JSON.
    /// </summary>
    /// <param name="directoryName">Directory name used under the test root.</param>
    /// <param name="manifestFileName">Manifest file name written under the root.</param>
    /// <param name="manifestJson">Manifest JSON to write.</param>
    /// <returns>Absolute path to the created shared toolchain root.</returns>
    string CreateSharedToolchainRoot(string directoryName, string manifestFileName, string manifestJson) {
        string sharedToolchainRootPath = Path.Combine(TempDirectoryPath, directoryName);
        Directory.CreateDirectory(sharedToolchainRootPath);
        File.WriteAllText(Path.Combine(sharedToolchainRootPath, manifestFileName), manifestJson);
        return sharedToolchainRootPath;
    }
}
