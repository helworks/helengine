namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes the resolved managed roots used for engine installs and shared toolchain installs.
/// </summary>
public sealed class LauncherInstallRoots {
    /// <summary>
    /// Initializes one resolved launcher install-root set.
    /// </summary>
    /// <param name="engineInstallRoot">Resolved engine install root path.</param>
    /// <param name="sharedToolchainRoot">Resolved shared toolchain root path.</param>
    public LauncherInstallRoots(string engineInstallRoot, string sharedToolchainRoot) {
        EngineInstallRoot = engineInstallRoot;
        SharedToolchainRoot = sharedToolchainRoot;
    }

    /// <summary>
    /// Gets the resolved engine install root path.
    /// </summary>
    public string EngineInstallRoot { get; }

    /// <summary>
    /// Gets the resolved shared toolchain root path.
    /// </summary>
    public string SharedToolchainRoot { get; }
}
