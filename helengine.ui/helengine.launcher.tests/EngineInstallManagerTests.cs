using System.Text.Json;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies launcher install state is persisted under managed engine and shared toolchain roots.
/// </summary>
public sealed class EngineInstallManagerTests : IDisposable {
    /// <summary>
    /// Stores the isolated temporary root used by the current test instance.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Creates one isolated temporary root directory for the current test instance.
    /// </summary>
    public EngineInstallManagerTests() {
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
    /// Ensures the manager restores installed engines from the managed engine root instead of a fixed application-settings path.
    /// </summary>
    [Fact]
    public void Load_WhenManagedRootContainsEngineManifest_RestoresInstalledEngines() {
        string engineRootPath = Path.Combine(TempRootPath, "engines");
        string toolchainRootPath = Path.Combine(TempRootPath, "toolchains");
        WriteEngineManifest(engineRootPath, "1.2.3");

        EngineInstallManager manager = CreateManager(engineRootPath, toolchainRootPath);

        EngineInstall install = Assert.Single(manager.InstalledEngines);
        Assert.Equal("1.2.3", install.Version);
    }

    /// <summary>
    /// Ensures installed artifacts persist under the managed shared toolchain root.
    /// </summary>
    [Fact]
    public void ReplaceInstalledArtifacts_WhenCalled_WritesSharedArtifactManifestUnderToolchainRoot() {
        string engineRootPath = Path.Combine(TempRootPath, "engines");
        string toolchainRootPath = Path.Combine(TempRootPath, "toolchains");
        EngineInstallManager manager = CreateManager(engineRootPath, toolchainRootPath);

        manager.ReplaceInstalledArtifacts(
            new[] {
                new InstalledArtifact(new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0"), Path.Combine(toolchainRootPath, "sdks", "android-sdk-34.0"))
            });

        Assert.True(File.Exists(Path.Combine(toolchainRootPath, "installed-artifacts.json")));
    }

    /// <summary>
    /// Ensures a fresh manager instance rediscovers installs and shared artifacts from the managed manifests.
    /// </summary>
    [Fact]
    public void CreateNewManager_WhenManagedManifestsExist_RediscoversPriorInstallState() {
        string engineRootPath = Path.Combine(TempRootPath, "engines");
        string toolchainRootPath = Path.Combine(TempRootPath, "toolchains");
        EngineInstallManager firstManager = CreateManager(engineRootPath, toolchainRootPath);
        firstManager.ReplaceInstalls(
            new[] {
                new EngineInstall {
                    Version = "2.0.0",
                    InstallPath = Path.Combine(engineRootPath, "helengine-2.0.0")
                }
            });
        firstManager.ReplaceInstalledArtifacts(
            new[] {
                new InstalledArtifact(new ArtifactIdentity(PlatformArtifactKind.PlatformBuilder, "windows-builder", "10.0"), Path.Combine(toolchainRootPath, "platform-builders", "windows-builder-10.0"))
            });

        EngineInstallManager secondManager = CreateManager(engineRootPath, toolchainRootPath);

        EngineInstall install = Assert.Single(secondManager.InstalledEngines);
        InstalledArtifact artifact = Assert.Single(secondManager.InstalledArtifacts);
        Assert.Equal("2.0.0", install.Version);
        Assert.Equal("windows-builder", artifact.Identity.Id);
    }

    /// <summary>
    /// Creates one engine install manager configured for the supplied managed roots.
    /// </summary>
    /// <param name="engineRootPath">Managed engine install root used by the test.</param>
    /// <param name="toolchainRootPath">Managed shared toolchain root used by the test.</param>
    /// <returns>Configured engine install manager.</returns>
    static EngineInstallManager CreateManager(string engineRootPath, string toolchainRootPath) {
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = engineRootPath,
            SharedToolchainRootPath = toolchainRootPath
        };
        LauncherInstallRootResolver resolver = new LauncherInstallRootResolver(locator);
        return new EngineInstallManager(resolver);
    }

    /// <summary>
    /// Writes one engine manifest directly into the managed engine root for load tests.
    /// </summary>
    /// <param name="engineRootPath">Managed engine install root that should receive the manifest.</param>
    /// <param name="version">Engine version stored in the manifest.</param>
    static void WriteEngineManifest(string engineRootPath, string version) {
        Directory.CreateDirectory(engineRootPath);
        EngineInstallManifest manifest = new EngineInstallManifest {
            Engines = new List<EngineInstall> {
                new EngineInstall {
                    Version = version,
                    InstallPath = Path.Combine(engineRootPath, $"helengine-{version}")
                }
            }
        };
        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(engineRootPath, "engines.json"), json);
    }
}
