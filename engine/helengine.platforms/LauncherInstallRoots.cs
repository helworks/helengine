namespace helengine.platforms;

/// <summary>
/// Stores the launcher-managed engine and shared-toolchain roots discovered on the current machine.
/// </summary>
public sealed class LauncherInstallRoots {
    /// <summary>
    /// Initializes one launcher install-roots instance.
    /// </summary>
    /// <param name="engineInstallRoot">Absolute engine install root discovered from launcher-managed state.</param>
    /// <param name="sharedToolchainRoot">Absolute shared toolchain root discovered from launcher-managed state.</param>
    public LauncherInstallRoots(string engineInstallRoot, string sharedToolchainRoot) {
        EngineInstallRoot = engineInstallRoot ?? string.Empty;
        SharedToolchainRoot = sharedToolchainRoot ?? string.Empty;
    }

    /// <summary>
    /// Gets the absolute engine install root discovered from launcher-managed state.
    /// </summary>
    public string EngineInstallRoot { get; }

    /// <summary>
    /// Gets the absolute shared toolchain root discovered from launcher-managed state.
    /// </summary>
    public string SharedToolchainRoot { get; }
}
