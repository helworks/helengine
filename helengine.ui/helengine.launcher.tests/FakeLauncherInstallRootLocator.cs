using helengine.editor.launcher.Services;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Provides an in-memory install-root locator for launcher root-resolution tests.
/// </summary>
public sealed class FakeLauncherInstallRootLocator : ILauncherInstallRootLocator {
    /// <summary>
    /// Gets or sets the stored engine install root path.
    /// </summary>
    public string EngineInstallRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stored shared toolchain root path.
    /// </summary>
    public string SharedToolchainRootPath { get; set; } = string.Empty;
}
