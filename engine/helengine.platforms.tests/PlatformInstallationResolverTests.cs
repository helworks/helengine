using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies engine-level manifest resolution returns platform entries with resolved payload paths and installation state.
/// </summary>
public sealed class PlatformInstallationResolverTests : IDisposable {
    /// <summary>
    /// Temporary root used for the resolver tests.
    /// </summary>
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory for the current test instance.
    /// </summary>
    public PlatformInstallationResolverTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "codex-helengine-platform-installation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the isolated temporary directory after the test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures one direct manifest entry is resolved relative to the manifest root and marked installed when its payload exists.
    /// </summary>
    [Fact]
    public void TryLoadPlatform_WhenManifestContainsInstalledEntry_ReturnsResolvedDescriptor() {
        string settingsRootPath = Path.Combine(TempDirectoryPath, "user_settings");
        Directory.CreateDirectory(settingsRootPath);

        File.WriteAllText(Path.Combine(settingsRootPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0-custom",
              "platformId": "windows",
              "displayName": "Windows DirectX",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-windows"
            }
          ]
        }
        """);

        string playerSourceRootPath = Path.GetFullPath(Path.Combine(settingsRootPath, "../helengine-windows"));
        Directory.CreateDirectory(playerSourceRootPath);

        PlatformInstallationResolver resolver = new PlatformInstallationResolver(settingsRootPath);

        bool resolved = resolver.TryLoadPlatform("1.0.0-custom", "windows", out AvailablePlatformDescriptor platform);

        Assert.True(resolved);
        Assert.Equal("windows", platform.Id);
        Assert.Equal("Windows DirectX", platform.DisplayName);
        Assert.Equal(string.Empty, platform.BuilderAssemblyPath);
        Assert.Equal(playerSourceRootPath, platform.PlayerSourceRootPath);
        Assert.True(platform.IsInstalled);
    }
}
