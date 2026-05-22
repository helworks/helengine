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

    /// <summary>
    /// Ensures one external plugin manifest that declares runtime payload CLR types is rejected at platform resolution time.
    /// </summary>
    [Fact]
    public void TryLoadPlatform_WhenPluginManifestContainsRuntimePayloadTypeMetadata_Throws() {
        string settingsRootPath = Path.Combine(TempDirectoryPath, "user_settings");
        Directory.CreateDirectory(settingsRootPath);
        string pluginRootPath = Path.Combine(TempDirectoryPath, "helengine-ps2");
        Directory.CreateDirectory(pluginRootPath);

        File.WriteAllText(Path.Combine(settingsRootPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0-custom",
              "platformId": "ps2",
              "displayName": "PlayStation 2",
              "builderAssemblyPath": "../helengine-ps2/builder/helengine.ps2.builder.dll",
              "playerSourceRootPath": "../helengine-ps2",
              "pluginManifestPath": "../helengine-ps2/platform-plugin.json"
            }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(pluginRootPath, "platform-plugin.json"), """
        {
          "platformId": "ps2",
          "displayName": "PlayStation 2",
          "runtimePayloadTypes": [ "helengine.ps2.Ps2MaterialAsset" ]
        }
        """);

        Directory.CreateDirectory(Path.Combine(pluginRootPath, "builder"));
        File.WriteAllText(Path.Combine(pluginRootPath, "builder", "helengine.ps2.builder.dll"), string.Empty);

        PlatformInstallationResolver resolver = new PlatformInstallationResolver(settingsRootPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => resolver.TryLoadPlatform("1.0.0-custom", "ps2", out _));
        Assert.Contains("runtime payload CLR types", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures one external plugin manifest that contains only generic metadata resolves successfully.
    /// </summary>
    [Fact]
    public void TryLoadPlatform_WhenPluginManifestContainsOnlyGenericMetadata_Succeeds() {
        string settingsRootPath = Path.Combine(TempDirectoryPath, "user_settings");
        Directory.CreateDirectory(settingsRootPath);
        string pluginRootPath = Path.Combine(TempDirectoryPath, "helengine-ps2");
        Directory.CreateDirectory(pluginRootPath);

        File.WriteAllText(Path.Combine(settingsRootPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0-custom",
              "platformId": "ps2",
              "displayName": "PlayStation 2",
              "builderAssemblyPath": "../helengine-ps2/builder/helengine.ps2.builder.dll",
              "playerSourceRootPath": "../helengine-ps2",
              "pluginManifestPath": "../helengine-ps2/platform-plugin.json"
            }
          ]
        }
        """);

        File.WriteAllText(Path.Combine(pluginRootPath, "platform-plugin.json"), """
        {
          "platformId": "ps2",
          "displayName": "PlayStation 2",
          "builderAssemblyPath": "builder/helengine.ps2.builder.dll",
          "generatedCoreProjectPaths": [ "managed/helengine.ps2/helengine.ps2.csproj" ]
        }
        """);

        Directory.CreateDirectory(Path.Combine(pluginRootPath, "builder"));
        File.WriteAllText(Path.Combine(pluginRootPath, "builder", "helengine.ps2.builder.dll"), string.Empty);
        Directory.CreateDirectory(Path.Combine(pluginRootPath, "managed", "helengine.ps2"));
        File.WriteAllText(Path.Combine(pluginRootPath, "managed", "helengine.ps2", "helengine.ps2.csproj"), "<Project />");

        PlatformInstallationResolver resolver = new PlatformInstallationResolver(settingsRootPath);

        bool resolved = resolver.TryLoadPlatform("1.0.0-custom", "ps2", out AvailablePlatformDescriptor platform);

        Assert.True(resolved);
        Assert.Equal("ps2", platform.Id);
        Assert.Equal("PlayStation 2", platform.DisplayName);
        Assert.Single(platform.GeneratedCoreProjectPaths);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(pluginRootPath, "managed", "helengine.ps2", "helengine.ps2.csproj")),
            platform.GeneratedCoreProjectPaths[0]);
    }
}
