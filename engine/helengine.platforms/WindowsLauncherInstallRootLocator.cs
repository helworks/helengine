using Microsoft.Win32;

namespace helengine.platforms;

/// <summary>
/// Reads launcher-managed install roots from the Windows registry without taking a dependency on launcher UI code.
/// </summary>
public class WindowsLauncherInstallRootLocator {
    /// <summary>
    /// Stores the registry key used by the launcher to persist install-root locators.
    /// </summary>
    const string LauncherRegistryKeyPath = "Software\\helengine\\launcher";

    /// <summary>
    /// Stores the registry value name used for the engine install root.
    /// </summary>
    const string EngineInstallRootValueName = "EngineInstallRoot";

    /// <summary>
    /// Stores the registry value name used for the shared toolchain root.
    /// </summary>
    const string SharedToolchainRootValueName = "SharedToolchainRoot";

    /// <summary>
    /// Loads the launcher-managed install roots from the Windows registry.
    /// </summary>
    /// <returns>Launcher-managed engine and shared-toolchain roots, or empty roots when registry state is unavailable.</returns>
    public virtual LauncherInstallRoots Load() {
        if (!OperatingSystem.IsWindows()) {
            return new LauncherInstallRoots(string.Empty, string.Empty);
        }

        using RegistryKey launcherKey = Registry.CurrentUser.OpenSubKey(LauncherRegistryKeyPath);
        if (launcherKey == null) {
            return new LauncherInstallRoots(string.Empty, string.Empty);
        }

        string engineInstallRoot = launcherKey.GetValue(EngineInstallRootValueName)?.ToString() ?? string.Empty;
        string sharedToolchainRoot = launcherKey.GetValue(SharedToolchainRootValueName)?.ToString() ?? string.Empty;
        return new LauncherInstallRoots(engineInstallRoot, sharedToolchainRoot);
    }
}
