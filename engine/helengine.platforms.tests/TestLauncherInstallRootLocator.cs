using helengine.platforms;

namespace helengine.platforms.tests;

/// <summary>
/// Supplies deterministic launcher install roots for platform-discovery tests without touching the real Windows registry.
/// </summary>
public sealed class TestLauncherInstallRootLocator : WindowsLauncherInstallRootLocator {
    readonly LauncherInstallRoots Roots;

    /// <summary>
    /// Captures the deterministic engine and toolchain roots returned by this test locator.
    /// </summary>
    /// <param name="engineInstallRoot">Engine install root returned to the resolver.</param>
    /// <param name="sharedToolchainRoot">Shared toolchain root returned to the resolver.</param>
    public TestLauncherInstallRootLocator(string engineInstallRoot, string sharedToolchainRoot) {
        Roots = new LauncherInstallRoots(engineInstallRoot, sharedToolchainRoot);
    }

    /// <summary>
    /// Returns the deterministic launcher install roots configured by the test.
    /// </summary>
    /// <returns>Deterministic launcher install roots.</returns>
    public override LauncherInstallRoots Load() {
        return Roots;
    }
}
