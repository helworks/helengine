using System.Collections.Generic;
using System.IO;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the install planner reports reusable, missing, and blocking shared artifact states for selected platforms.
/// </summary>
public sealed class PlatformInstallPlannerTests : IDisposable {
    /// <summary>
    /// Stores the isolated temporary root used to materialize reusable artifact folders for the current test.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Creates one isolated temporary root used by the current test instance.
    /// </summary>
    public PlatformInstallPlannerTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-launcher-tests", "planner", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    /// <summary>
    /// Deletes the isolated temporary root after the current test finishes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures exact installed artifacts are marked reusable instead of missing.
    /// </summary>
    [Fact]
    public void Build_WhenExactSdkAlreadyExists_MarksItReusable() {
        MockEnginePlatformCatalog catalog = new MockEnginePlatformCatalog();
        string installPath = Path.Combine(TempRootPath, "sdks", "android-sdk-34.0");
        Directory.CreateDirectory(installPath);
        PlatformInstallPlanner planner = new PlatformInstallPlanner(
            catalog,
            new[] {
                new InstalledArtifact(new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0"), installPath)
            });

        PlatformInstallPlan plan = planner.Build(new PlatformInstallSelection("1.2.3", new[] { "android" }));

        Assert.Contains(plan.ReusableArtifacts, item => item.Identity.Id == "android-sdk");
        Assert.Contains(plan.MissingArtifacts, item => item.Identity.Id == "android-builder");
        Assert.Empty(plan.BlockingIssues);
    }

    /// <summary>
    /// Ensures missing artifacts remain in the missing set when the toolchain root does not contain them yet.
    /// </summary>
    [Fact]
    public void Build_WhenRequiredArtifactsAreMissing_MarksThemMissing() {
        MockEnginePlatformCatalog catalog = new MockEnginePlatformCatalog();
        PlatformInstallPlanner planner = new PlatformInstallPlanner(catalog, new List<InstalledArtifact>());

        PlatformInstallPlan plan = planner.Build(new PlatformInstallSelection("1.2.3", new[] { "windows" }));

        Assert.Contains(plan.MissingArtifacts, item => item.Identity.Id == "windows-sdk");
        Assert.Contains(plan.MissingArtifacts, item => item.Identity.Id == "windows-builder");
        Assert.Contains(plan.MissingArtifacts, item => item.Identity.Id == "windows-platform-files");
        Assert.Empty(plan.BlockingIssues);
    }

    /// <summary>
    /// Ensures a manifest entry that points to a missing install path blocks the plan instead of being silently reused.
    /// </summary>
    [Fact]
    public void Build_WhenInstalledArtifactPathIsMissing_AddsBlockingIssue() {
        MockEnginePlatformCatalog catalog = new MockEnginePlatformCatalog();
        PlatformInstallPlanner planner = new PlatformInstallPlanner(
            catalog,
            new[] {
                new InstalledArtifact(
                    new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0"),
                    Path.Combine(TempRootPath, "sdks", "missing-android-sdk"))
            });

        PlatformInstallPlan plan = planner.Build(new PlatformInstallSelection("1.2.3", new[] { "android" }));

        Assert.Contains(plan.BlockingIssues, item => item.Contains("android-sdk"));
    }
}
