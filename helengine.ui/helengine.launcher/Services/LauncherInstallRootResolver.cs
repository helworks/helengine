using System;
using System.IO;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Resolves the effective launcher install roots by combining persisted locators with the default helengine roaming paths.
/// </summary>
public sealed class LauncherInstallRootResolver {
    /// <summary>
    /// Stores the persisted locator source used to resolve effective install roots.
    /// </summary>
    readonly ILauncherInstallRootLocator Locator;

    /// <summary>
    /// Initializes one launcher install-root resolver.
    /// </summary>
    /// <param name="locator">Persisted locator source that stores user-selected roots.</param>
    public LauncherInstallRootResolver(ILauncherInstallRootLocator locator) {
        Locator = locator;
    }

    /// <summary>
    /// Resolves the effective engine install root and shared toolchain root for the launcher.
    /// </summary>
    /// <returns>Resolved launcher install roots.</returns>
    public LauncherInstallRoots Resolve() {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string defaultHelengineRoot = Path.Combine(appData, "helengine");
        string engineInstallRoot = string.IsNullOrWhiteSpace(Locator.EngineInstallRootPath)
            ? Path.Combine(defaultHelengineRoot, "engines")
            : Locator.EngineInstallRootPath;
        string sharedToolchainRoot = string.IsNullOrWhiteSpace(Locator.SharedToolchainRootPath)
            ? Path.Combine(defaultHelengineRoot, "toolchains")
            : Locator.SharedToolchainRootPath;
        return new LauncherInstallRoots(engineInstallRoot, sharedToolchainRoot);
    }
}
