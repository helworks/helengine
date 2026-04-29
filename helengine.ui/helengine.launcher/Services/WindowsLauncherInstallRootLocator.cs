using Microsoft.Win32;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Persists launcher install-root locators in the current-user Windows registry hive.
/// </summary>
public sealed class WindowsLauncherInstallRootLocator : ILauncherInstallRootLocator {
    /// <summary>
    /// Stores the registry key path that contains the launcher root-locator values.
    /// </summary>
    const string RegistryKeyPath = @"Software\helengine\launcher";

    /// <summary>
    /// Stores the registry value name used for the engine install root.
    /// </summary>
    const string EngineInstallRootValueName = "EngineInstallRoot";

    /// <summary>
    /// Stores the registry value name used for the shared toolchain root.
    /// </summary>
    const string SharedToolchainRootValueName = "SharedToolchainRoot";

    /// <summary>
    /// Gets or sets the persisted engine install root path.
    /// </summary>
    public string EngineInstallRootPath {
        get {
            return ReadValue(EngineInstallRootValueName);
        }
        set {
            WriteValue(EngineInstallRootValueName, value);
        }
    }

    /// <summary>
    /// Gets or sets the persisted shared toolchain root path.
    /// </summary>
    public string SharedToolchainRootPath {
        get {
            return ReadValue(SharedToolchainRootValueName);
        }
        set {
            WriteValue(SharedToolchainRootValueName, value);
        }
    }

    /// <summary>
    /// Reads one string locator value from the current-user registry hive.
    /// </summary>
    /// <param name="valueName">Registry value name to read.</param>
    /// <returns>Stored locator path or an empty string when no value exists.</returns>
    string ReadValue(string valueName) {
        RegistryKey currentUser = Registry.CurrentUser;
        if (currentUser == null) {
            return string.Empty;
        }

        using RegistryKey key = currentUser.OpenSubKey(RegistryKeyPath)!;
        if (key == null) {
            return string.Empty;
        }

        object value = key.GetValue(valueName)!;
        if (value == null) {
            return string.Empty;
        }

        if (value is string path) {
            return path;
        }

        return string.Empty;
    }

    /// <summary>
    /// Writes one string locator value into the current-user registry hive.
    /// </summary>
    /// <param name="valueName">Registry value name to write.</param>
    /// <param name="value">Locator path to store.</param>
    void WriteValue(string valueName, string value) {
        RegistryKey currentUser = Registry.CurrentUser;
        if (currentUser == null) {
            return;
        }

        using RegistryKey key = currentUser.CreateSubKey(RegistryKeyPath)!;
        key.SetValue(valueName, value ?? string.Empty, RegistryValueKind.String);
    }
}
