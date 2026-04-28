namespace helengine.editor.launcher.Services;

/// <summary>
/// Provides access to the persisted launcher install-root locator values.
/// </summary>
public interface ILauncherInstallRootLocator {
    /// <summary>
    /// Gets or sets the persisted engine install root path.
    /// </summary>
    string EngineInstallRootPath { get; set; }

    /// <summary>
    /// Gets or sets the persisted shared toolchain root path.
    /// </summary>
    string SharedToolchainRootPath { get; set; }
}
