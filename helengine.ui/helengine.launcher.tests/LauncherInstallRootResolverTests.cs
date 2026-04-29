using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies launcher install-root resolution keeps engine and shared toolchain paths independent.
/// </summary>
public sealed class LauncherInstallRootResolverTests {
    /// <summary>
    /// Ensures missing locator values fall back to the default helengine engine and toolchain roots under the roaming application-data directory.
    /// </summary>
    [Fact]
    public void Resolve_WhenNoLocatorValuesExist_UsesDefaultHelenginePaths() {
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator();
        LauncherInstallRootResolver resolver = new LauncherInstallRootResolver(locator);

        LauncherInstallRoots roots = resolver.Resolve();

        Assert.EndsWith(Path.Combine("helengine", "engines"), roots.EngineInstallRoot);
        Assert.EndsWith(Path.Combine("helengine", "toolchains"), roots.SharedToolchainRoot);
    }

    /// <summary>
    /// Ensures explicitly chosen engine and shared toolchain roots are preserved exactly instead of being replaced by defaults.
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitRootsExist_PreservesConfiguredValues() {
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = @"D:\helengine\engines",
            SharedToolchainRootPath = @"E:\helengine\toolchains"
        };
        LauncherInstallRootResolver resolver = new LauncherInstallRootResolver(locator);

        LauncherInstallRoots roots = resolver.Resolve();

        Assert.Equal(@"D:\helengine\engines", roots.EngineInstallRoot);
        Assert.Equal(@"E:\helengine\toolchains", roots.SharedToolchainRoot);
    }

    /// <summary>
    /// Ensures the locator keeps engine-root and shared-toolchain-root values independent from each other.
    /// </summary>
    [Fact]
    public void Resolve_WhenRootsDiffer_TracksEngineAndToolchainRootsSeparately() {
        FakeLauncherInstallRootLocator locator = new FakeLauncherInstallRootLocator {
            EngineInstallRootPath = @"D:\portable\engine-installs",
            SharedToolchainRootPath = @"D:\portable\shared-toolchains"
        };
        LauncherInstallRootResolver resolver = new LauncherInstallRootResolver(locator);

        LauncherInstallRoots roots = resolver.Resolve();

        Assert.NotEqual(roots.EngineInstallRoot, roots.SharedToolchainRoot);
        Assert.Equal(@"D:\portable\engine-installs", roots.EngineInstallRoot);
        Assert.Equal(@"D:\portable\shared-toolchains", roots.SharedToolchainRoot);
    }
}
