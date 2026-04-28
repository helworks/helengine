using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Removes installed engines and optionally deletes newly unused shared artifacts from the managed launcher roots.
/// </summary>
public sealed class EngineUninstallExecutor {
    /// <summary>
    /// Stores the manifest-backed install manager updated by successful uninstalls.
    /// </summary>
    EngineInstallManager InstallManager { get; }

    /// <summary>
    /// Stores the uninstall planner that computes newly unused shared artifacts.
    /// </summary>
    EngineUninstallPlanner Planner { get; }

    /// <summary>
    /// Creates one uninstall executor for the supplied manifest-backed manager and unused-artifact planner.
    /// </summary>
    /// <param name="installManager">Manifest-backed manager that should be updated after uninstall.</param>
    /// <param name="planner">Planner that computes newly unused shared artifacts.</param>
    public EngineUninstallExecutor(EngineInstallManager installManager, EngineUninstallPlanner planner) {
        InstallManager = installManager;
        Planner = planner;
    }

    /// <summary>
    /// Removes the supplied engine version and optionally deletes the shared artifacts that become unused afterwards.
    /// </summary>
    /// <param name="engineVersion">Exact engine version to remove.</param>
    /// <param name="removeUnusedArtifacts"><c>true</c> when newly unused shared artifacts should also be removed; otherwise <c>false</c>.</param>
    public void Uninstall(string engineVersion, bool removeUnusedArtifacts) {
        UnusedArtifactRemovalDecision unusedArtifacts = Planner.GetUnusedArtifactsAfterRemoving(engineVersion);
        List<EngineInstall> installedEngines = InstallManager.InstalledEngines.ToList();
        string engineInstallPath = RemoveInstalledEngine(installedEngines, engineVersion);
        DeleteDirectoryIfPresent(engineInstallPath);
        InstallManager.ReplaceInstalls(installedEngines);

        List<InstalledEnginePlatformBinding> installedBindings = InstallManager.InstalledBindings
            .Where(binding => !string.Equals(binding.EngineVersion, engineVersion, StringComparison.Ordinal))
            .ToList();
        InstallManager.ReplaceInstalledBindings(installedBindings);

        if (removeUnusedArtifacts) {
            RemoveUnusedArtifacts(unusedArtifacts.UnusedArtifacts);
        }
    }

    /// <summary>
    /// Removes one installed engine entry from the tracked install list and returns its install path.
    /// </summary>
    /// <param name="installedEngines">Installed engine entries being updated.</param>
    /// <param name="engineVersion">Exact engine version to remove.</param>
    /// <returns>Managed engine install path of the removed engine.</returns>
    static string RemoveInstalledEngine(List<EngineInstall> installedEngines, string engineVersion) {
        for (int index = 0; index < installedEngines.Count; index++) {
            EngineInstall install = installedEngines[index];
            if (string.Equals(install.Version, engineVersion, StringComparison.Ordinal)) {
                installedEngines.RemoveAt(index);
                return install.InstallPath;
            }
        }

        throw new InvalidOperationException($"Engine version '{engineVersion}' is not installed.");
    }

    /// <summary>
    /// Removes the supplied newly unused shared artifacts from disk and from the persisted manifest state.
    /// </summary>
    /// <param name="unusedArtifacts">Newly unused shared artifacts confirmed for cleanup.</param>
    void RemoveUnusedArtifacts(IReadOnlyList<InstalledArtifact> unusedArtifacts) {
        List<InstalledArtifact> installedArtifacts = InstallManager.InstalledArtifacts.ToList();
        for (int index = 0; index < unusedArtifacts.Count; index++) {
            InstalledArtifact unusedArtifact = unusedArtifacts[index];
            DeleteDirectoryIfPresent(unusedArtifact.InstallPath);
            RemoveInstalledArtifact(installedArtifacts, unusedArtifact.Identity);
        }

        InstallManager.ReplaceInstalledArtifacts(installedArtifacts);
    }

    /// <summary>
    /// Removes one exact reusable artifact identity from the installed-artifact list.
    /// </summary>
    /// <param name="installedArtifacts">Installed shared artifacts being updated.</param>
    /// <param name="identity">Exact reusable artifact identity to remove.</param>
    static void RemoveInstalledArtifact(List<InstalledArtifact> installedArtifacts, ArtifactIdentity identity) {
        for (int index = installedArtifacts.Count - 1; index >= 0; index--) {
            if (installedArtifacts[index].Identity.Equals(identity)) {
                installedArtifacts.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// Deletes the supplied directory when it exists on disk.
    /// </summary>
    /// <param name="path">Directory path that should be removed.</param>
    static void DeleteDirectoryIfPresent(string path) {
        if (Directory.Exists(path)) {
            Directory.Delete(path, true);
        }
    }
}
