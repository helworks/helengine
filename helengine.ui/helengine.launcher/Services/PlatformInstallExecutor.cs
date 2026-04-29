using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Materializes mocked engine installs and shared platform artifacts under the managed launcher roots.
/// </summary>
public sealed class PlatformInstallExecutor {
    /// <summary>
    /// Stores the catalog used to resolve engine-platform requirements.
    /// </summary>
    IEnginePlatformCatalog Catalog { get; }

    /// <summary>
    /// Stores the manifest-backed install manager updated by successful mocked installs.
    /// </summary>
    EngineInstallManager InstallManager { get; }

    /// <summary>
    /// Creates one mocked install executor for the supplied catalog and manifest-backed manager.
    /// </summary>
    /// <param name="catalog">Catalog that describes installable engine-platform requirements.</param>
    /// <param name="installManager">Manifest-backed manager that should be updated after install.</param>
    public PlatformInstallExecutor(IEnginePlatformCatalog catalog, EngineInstallManager installManager) {
        Catalog = catalog;
        InstallManager = installManager;
    }

    /// <summary>
    /// Materializes the selected engine and platform artifacts under the managed launcher roots and persists the updated manifests.
    /// </summary>
    /// <param name="selection">Engine version and platforms that should be installed.</param>
    public void Install(PlatformInstallSelection selection) {
        PlatformInstallPlanner planner = new PlatformInstallPlanner(Catalog, InstallManager.InstalledArtifacts);
        PlatformInstallPlan plan = planner.Build(selection);
        if (plan.BlockingIssues.Count > 0) {
            throw new InvalidOperationException(string.Join(Environment.NewLine, plan.BlockingIssues));
        }

        EngineCatalogEntry engineEntry = ResolveEngineEntry(selection.EngineVersion);
        string engineInstallPath = BuildEngineInstallPath(selection.EngineVersion);
        Directory.CreateDirectory(engineInstallPath);

        List<InstalledArtifact> installedArtifacts = InstallManager.InstalledArtifacts.ToList();
        List<InstalledEnginePlatformBinding> installedBindings = InstallManager.InstalledBindings.ToList();
        foreach (string platformId in selection.PlatformIds) {
            EnginePlatformRequirement requirement = ResolvePlatformRequirement(engineEntry, platformId);
            EnsureArtifactInstalled(installedArtifacts, requirement.Sdk.Identity);
            EnsureArtifactInstalled(installedArtifacts, requirement.PlatformBuilder.Identity);
            EnsureArtifactInstalled(installedArtifacts, requirement.PlatformFiles.Identity);

            RemoveBinding(installedBindings, selection.EngineVersion, platformId);
            installedBindings.Add(
                new InstalledEnginePlatformBinding(
                    selection.EngineVersion,
                    platformId,
                    requirement.Sdk.Identity,
                    requirement.PlatformBuilder.Identity,
                    requirement.PlatformFiles.Identity));
        }

        ReplaceInstalledEngine(selection.EngineVersion, engineInstallPath);
        InstallManager.ReplaceInstalledArtifacts(installedArtifacts);
        InstallManager.ReplaceInstalledBindings(installedBindings);
    }

    /// <summary>
    /// Resolves one engine catalog entry by exact version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version to resolve.</param>
    /// <returns>Matching engine catalog entry.</returns>
    EngineCatalogEntry ResolveEngineEntry(string engineVersion) {
        for (int index = 0; index < Catalog.GetAvailableEngines().Count; index++) {
            EngineCatalogEntry entry = Catalog.GetAvailableEngines()[index];
            if (string.Equals(entry.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                return entry;
            }
        }

        throw new InvalidOperationException($"Engine version '{engineVersion}' does not exist in the platform catalog.");
    }

    /// <summary>
    /// Resolves one platform requirement from the supplied engine catalog entry.
    /// </summary>
    /// <param name="engineEntry">Engine catalog entry that owns the platform requirement.</param>
    /// <param name="platformId">Stable platform identifier to resolve.</param>
    /// <returns>Matching platform requirement.</returns>
    static EnginePlatformRequirement ResolvePlatformRequirement(EngineCatalogEntry engineEntry, string platformId) {
        for (int index = 0; index < engineEntry.PlatformRequirements.Count; index++) {
            EnginePlatformRequirement requirement = engineEntry.PlatformRequirements[index];
            if (string.Equals(requirement.PlatformId, platformId, StringComparison.Ordinal)) {
                return requirement;
            }
        }

        throw new InvalidOperationException($"Platform '{platformId}' is not available for engine version '{engineEntry.EngineVersion}'.");
    }

    /// <summary>
    /// Builds the managed install path for the supplied engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version that should be materialized.</param>
    /// <returns>Managed engine install path.</returns>
    string BuildEngineInstallPath(string engineVersion) {
        return Path.Combine(InstallManager.InstallRoots.EngineInstallRoot, $"helengine-{engineVersion}");
    }

    /// <summary>
    /// Ensures one exact shared artifact exists both on disk and in the installed-artifact manifest.
    /// </summary>
    /// <param name="installedArtifacts">Installed shared artifacts being updated for the current install.</param>
    /// <param name="identity">Exact reusable artifact identity to materialize.</param>
    void EnsureArtifactInstalled(List<InstalledArtifact> installedArtifacts, ArtifactIdentity identity) {
        int existingArtifactIndex = FindInstalledArtifactIndex(installedArtifacts, identity);
        if (existingArtifactIndex >= 0) {
            return;
        }

        string installPath = BuildArtifactInstallPath(identity);
        Directory.CreateDirectory(installPath);
        installedArtifacts.Add(new InstalledArtifact(identity, installPath));
    }

    /// <summary>
    /// Builds the managed install path for the supplied shared artifact identity.
    /// </summary>
    /// <param name="identity">Exact reusable artifact identity to materialize.</param>
    /// <returns>Managed shared-artifact install path.</returns>
    string BuildArtifactInstallPath(ArtifactIdentity identity) {
        return Path.Combine(InstallManager.InstallRoots.SharedToolchainRoot, GetArtifactFolderName(identity.Kind), $"{identity.Id}-{identity.Version}");
    }

    /// <summary>
    /// Resolves the shared-toolchain subfolder used by one artifact family.
    /// </summary>
    /// <param name="kind">Artifact family managed by the launcher.</param>
    /// <returns>Shared-toolchain subfolder for the artifact family.</returns>
    static string GetArtifactFolderName(PlatformArtifactKind kind) {
        if (kind == PlatformArtifactKind.Sdk) {
            return "sdks";
        }

        if (kind == PlatformArtifactKind.PlatformBuilder) {
            return "platform-builders";
        }

        return "platform-files";
    }

    /// <summary>
    /// Finds the zero-based installed-artifact index for the supplied exact reusable identity.
    /// </summary>
    /// <param name="installedArtifacts">Installed shared artifacts being searched.</param>
    /// <param name="identity">Exact reusable artifact identity to match.</param>
    /// <returns>Matching installed-artifact index or <c>-1</c> when no match exists.</returns>
    static int FindInstalledArtifactIndex(IReadOnlyList<InstalledArtifact> installedArtifacts, ArtifactIdentity identity) {
        for (int index = 0; index < installedArtifacts.Count; index++) {
            if (installedArtifacts[index].Identity.Equals(identity)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Removes one existing engine-platform binding before a replacement binding is added.
    /// </summary>
    /// <param name="installedBindings">Installed bindings being updated for the current install.</param>
    /// <param name="engineVersion">Exact engine version that owns the binding.</param>
    /// <param name="platformId">Stable platform identifier that owns the binding.</param>
    static void RemoveBinding(List<InstalledEnginePlatformBinding> installedBindings, string engineVersion, string platformId) {
        for (int index = installedBindings.Count - 1; index >= 0; index--) {
            InstalledEnginePlatformBinding binding = installedBindings[index];
            if (string.Equals(binding.EngineVersion, engineVersion, StringComparison.Ordinal)
                && string.Equals(binding.PlatformId, platformId, StringComparison.Ordinal)) {
                installedBindings.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// Replaces or adds the installed engine entry for the supplied engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version that was installed.</param>
    /// <param name="engineInstallPath">Managed engine install path that was materialized.</param>
    void ReplaceInstalledEngine(string engineVersion, string engineInstallPath) {
        List<EngineInstall> installedEngines = InstallManager.InstalledEngines.ToList();
        EngineInstall install = new EngineInstall {
            Version = engineVersion,
            InstallPath = engineInstallPath,
            Source = "catalog",
            InstalledAt = DateTime.Now,
            Name = $"helengine {engineVersion}"
        };

        for (int index = 0; index < installedEngines.Count; index++) {
            if (string.Equals(installedEngines[index].Version, engineVersion, StringComparison.Ordinal)) {
                installedEngines[index] = install;
                InstallManager.ReplaceInstalls(installedEngines);
                return;
            }
        }

        installedEngines.Insert(0, install);
        InstallManager.ReplaceInstalls(installedEngines);
    }
}
