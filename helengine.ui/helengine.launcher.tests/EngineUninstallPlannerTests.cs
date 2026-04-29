using System.Linq;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies unused shared-artifact detection and uninstall cleanup behavior for catalog-managed engine installs.
/// </summary>
public sealed class EngineUninstallPlannerTests : IDisposable {
    /// <summary>
    /// Stores the isolated temporary root used by the current test instance.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Creates one isolated temporary root directory for the current test instance.
    /// </summary>
    public EngineUninstallPlannerTests() {
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
    /// Ensures the planner reports shared artifacts that would become unreferenced after removing one engine.
    /// </summary>
    [Fact]
    public void GetUnusedArtifactsAfterRemoving_WhenRemovedEngineWasLastReference_ReturnsArtifactsForPrompt() {
        EngineInstallManager manager = CreateManager();
        InstallEngine(manager, "1.2.3", "android");
        EngineUninstallPlanner planner = new EngineUninstallPlanner(manager.InstalledArtifacts, manager.InstalledBindings);

        UnusedArtifactRemovalDecision decision = planner.GetUnusedArtifactsAfterRemoving("1.2.3");

        Assert.Equal("1.2.3", decision.EngineVersion);
        Assert.Equal(3, decision.UnusedArtifacts.Count);
        Assert.Contains(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-sdk");
        Assert.Contains(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-builder");
        Assert.Contains(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-platform-files");
    }

    /// <summary>
    /// Ensures shared artifacts that remain referenced by another installed engine are not reported as unused.
    /// </summary>
    [Fact]
    public void GetUnusedArtifactsAfterRemoving_WhenAnotherEngineStillReferencesSharedArtifact_ExcludesThatArtifact() {
        EngineInstallManager manager = CreateManager();
        InstallEngine(manager, "1.2.3", "android");
        InstallEngine(manager, "2.0.0", "android");
        EngineUninstallPlanner planner = new EngineUninstallPlanner(manager.InstalledArtifacts, manager.InstalledBindings);

        UnusedArtifactRemovalDecision decision = planner.GetUnusedArtifactsAfterRemoving("1.2.3");

        Assert.DoesNotContain(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-sdk");
        Assert.Contains(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-builder");
        Assert.Contains(decision.UnusedArtifacts, artifact => artifact.Identity.Id == "android-platform-files");
    }

    /// <summary>
    /// Ensures uninstall can keep newly unused shared artifacts when the user declines cleanup.
    /// </summary>
    [Fact]
    public void Uninstall_WhenRemoveUnusedArtifactsIsFalse_DeletesEngineButKeepsSharedArtifacts() {
        EngineInstallManager manager = CreateManager();
        InstallEngine(manager, "1.2.3", "android");
        EngineUninstallExecutor executor = new EngineUninstallExecutor(manager, new EngineUninstallPlanner(manager.InstalledArtifacts, manager.InstalledBindings));

        executor.Uninstall("1.2.3", false);

        Assert.Empty(manager.InstalledEngines);
        Assert.Empty(manager.InstalledBindings);
        Assert.Equal(3, manager.InstalledArtifacts.Count);
        Assert.All(manager.InstalledArtifacts, artifact => Assert.True(Directory.Exists(artifact.InstallPath)));
    }

    /// <summary>
    /// Ensures uninstall removes newly unused shared artifacts when the user confirms cleanup.
    /// </summary>
    [Fact]
    public void Uninstall_WhenRemoveUnusedArtifactsIsTrue_DeletesNewlyUnusedArtifacts() {
        EngineInstallManager manager = CreateManager();
        InstallEngine(manager, "1.2.3", "android");
        EngineUninstallExecutor executor = new EngineUninstallExecutor(manager, new EngineUninstallPlanner(manager.InstalledArtifacts, manager.InstalledBindings));

        executor.Uninstall("1.2.3", true);

        Assert.Empty(manager.InstalledEngines);
        Assert.Empty(manager.InstalledBindings);
        Assert.Empty(manager.InstalledArtifacts);
        Assert.False(Directory.Exists(Path.Combine(manager.InstallRoots.SharedToolchainRoot, "sdks", "android-sdk-34.0")));
    }

    /// <summary>
    /// Creates one engine install manager configured for an isolated pair of managed roots.
    /// </summary>
    /// <returns>Configured engine install manager.</returns>
    EngineInstallManager CreateManager() {
        string engineRootPath = Path.Combine(TempRootPath, "engines");
        string toolchainRootPath = Path.Combine(TempRootPath, "toolchains");
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = engineRootPath,
            SharedToolchainRootPath = toolchainRootPath
        };
        return new EngineInstallManager(new LauncherInstallRootResolver(locator));
    }

    /// <summary>
    /// Installs one engine version for the supplied platform selection into the isolated manager roots.
    /// </summary>
    /// <param name="manager">Engine install manager that should receive the mocked install.</param>
    /// <param name="engineVersion">Exact engine version to install.</param>
    /// <param name="platformId">Single platform identifier that should be installed.</param>
    void InstallEngine(EngineInstallManager manager, string engineVersion, string platformId) {
        PlatformInstallExecutor executor = new PlatformInstallExecutor(new MockEnginePlatformCatalog(), manager);
        executor.Install(new PlatformInstallSelection(engineVersion, new[] { platformId }));
    }
}
