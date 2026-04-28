using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies mocked engine and shared-artifact installs are materialized under the managed launcher roots.
/// </summary>
public sealed class PlatformInstallExecutorTests : IDisposable {
    /// <summary>
    /// Stores the isolated temporary root used by the current test instance.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Creates one isolated temporary root directory for the current test instance.
    /// </summary>
    public PlatformInstallExecutorTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    /// <summary>
    /// Deletes the isolated temporary root after the current test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures mocked install execution creates the managed engine and shared-artifact folders and persists manifest state.
    /// </summary>
    [Fact]
    public void Install_WhenSelectionIsValid_CreatesManagedFoldersAndPersistsManifestState() {
        EngineInstallManager manager = CreateManager(out string engineRootPath, out string toolchainRootPath);
        PlatformInstallExecutor executor = new PlatformInstallExecutor(new MockEnginePlatformCatalog(), manager);

        executor.Install(new PlatformInstallSelection("1.2.3", new[] { "android", "windows" }));

        Assert.True(Directory.Exists(Path.Combine(engineRootPath, "helengine-1.2.3")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "sdks", "android-sdk-34.0")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "sdks", "windows-sdk-10.0")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "platform-builders", "android-builder-1.2.3")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "platform-builders", "windows-builder-1.2.3")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "platform-files", "android-platform-files-1.2.3")));
        Assert.True(Directory.Exists(Path.Combine(toolchainRootPath, "platform-files", "windows-platform-files-1.2.3")));

        EngineInstall install = Assert.Single(manager.InstalledEngines);
        Assert.Equal("1.2.3", install.Version);
        Assert.Equal(Path.Combine(engineRootPath, "helengine-1.2.3"), install.InstallPath);
        Assert.Equal(6, manager.InstalledArtifacts.Count);
        Assert.Equal(2, manager.InstalledBindings.Count);
        Assert.True(File.Exists(Path.Combine(engineRootPath, "engines.json")));
        Assert.True(File.Exists(Path.Combine(toolchainRootPath, "installed-artifacts.json")));
        Assert.True(File.Exists(Path.Combine(toolchainRootPath, "installed-bindings.json")));
    }

    /// <summary>
    /// Creates one engine install manager configured for an isolated pair of managed roots.
    /// </summary>
    /// <param name="engineRootPath">Receives the managed engine install root.</param>
    /// <param name="toolchainRootPath">Receives the managed shared toolchain root.</param>
    /// <returns>Configured engine install manager.</returns>
    EngineInstallManager CreateManager(out string engineRootPath, out string toolchainRootPath) {
        engineRootPath = Path.Combine(TempRootPath, "engines");
        toolchainRootPath = Path.Combine(TempRootPath, "toolchains");
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = engineRootPath,
            SharedToolchainRootPath = toolchainRootPath
        };
        return new EngineInstallManager(new LauncherInstallRootResolver(locator));
    }
}
