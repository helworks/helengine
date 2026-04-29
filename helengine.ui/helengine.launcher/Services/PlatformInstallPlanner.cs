using System.Collections.Generic;
using System.IO;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Compares one engine-platform install selection against the installed shared artifact registry and reports reusable, missing, and blocking items.
/// </summary>
public sealed class PlatformInstallPlanner {
    /// <summary>
    /// Stores the catalog used to resolve engine-platform requirements.
    /// </summary>
    IEnginePlatformCatalog Catalog { get; }

    /// <summary>
    /// Stores the installed shared artifacts currently known to the launcher.
    /// </summary>
    IReadOnlyList<InstalledArtifact> InstalledArtifacts { get; }

    /// <summary>
    /// Initializes one install planner for the supplied catalog and installed shared artifact registry.
    /// </summary>
    /// <param name="catalog">Catalog that describes installable engine-platform requirements.</param>
    /// <param name="installedArtifacts">Installed shared artifacts currently available for reuse.</param>
    public PlatformInstallPlanner(IEnginePlatformCatalog catalog, IEnumerable<InstalledArtifact> installedArtifacts) {
        Catalog = catalog;
        InstalledArtifacts = new List<InstalledArtifact>(installedArtifacts);
    }

    /// <summary>
    /// Builds one install plan for the supplied engine version and platform selection.
    /// </summary>
    /// <param name="selection">User-selected engine version and platforms to evaluate.</param>
    /// <returns>Pure planning result with reusable, missing, and blocking entries.</returns>
    public PlatformInstallPlan Build(PlatformInstallSelection selection) {
        PlatformInstallPlan plan = new PlatformInstallPlan();
        int engineEntryIndex = FindEngineEntryIndex(selection.EngineVersion);
        if (engineEntryIndex < 0) {
            plan.BlockingIssues.Add($"Engine version '{selection.EngineVersion}' does not exist in the platform catalog.");
            return plan;
        }

        EngineCatalogEntry engineEntry = Catalog.GetAvailableEngines()[engineEntryIndex];
        foreach (string platformId in selection.PlatformIds) {
            int platformRequirementIndex = FindPlatformRequirementIndex(engineEntry, platformId);
            if (platformRequirementIndex < 0) {
                plan.BlockingIssues.Add($"Platform '{platformId}' is not available for engine version '{selection.EngineVersion}'.");
            } else {
                EnginePlatformRequirement platformRequirement = engineEntry.PlatformRequirements[platformRequirementIndex];
                AddArtifactStatus(plan, platformId, platformRequirement.Sdk.Identity);
                AddArtifactStatus(plan, platformId, platformRequirement.PlatformBuilder.Identity);
                AddArtifactStatus(plan, platformId, platformRequirement.PlatformFiles.Identity);
            }
        }

        return plan;
    }

    /// <summary>
    /// Finds the zero-based engine catalog entry index for the supplied engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version to match.</param>
    /// <returns>Matching engine catalog entry index or <c>-1</c> when the version is not available.</returns>
    int FindEngineEntryIndex(string engineVersion) {
        IReadOnlyList<EngineCatalogEntry> entries = Catalog.GetAvailableEngines();
        for (int index = 0; index < entries.Count; index++) {
            if (string.Equals(entries[index].EngineVersion, engineVersion, System.StringComparison.Ordinal)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds the zero-based platform requirement index under the supplied engine catalog entry.
    /// </summary>
    /// <param name="engineEntry">Engine catalog entry that owns the platform requirements.</param>
    /// <param name="platformId">Stable platform identifier to match.</param>
    /// <returns>Matching platform requirement index or <c>-1</c> when the engine does not expose that platform.</returns>
    int FindPlatformRequirementIndex(EngineCatalogEntry engineEntry, string platformId) {
        for (int index = 0; index < engineEntry.PlatformRequirements.Count; index++) {
            if (string.Equals(engineEntry.PlatformRequirements[index].PlatformId, platformId, System.StringComparison.Ordinal)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Adds one reusable, missing, or blocking row for the supplied artifact identity.
    /// </summary>
    /// <param name="plan">Plan being built.</param>
    /// <param name="platformId">Platform that requires the artifact.</param>
    /// <param name="identity">Exact reusable artifact identity to evaluate.</param>
    void AddArtifactStatus(PlatformInstallPlan plan, string platformId, ArtifactIdentity identity) {
        int installedArtifactIndex = FindInstalledArtifactIndex(identity);
        if (installedArtifactIndex < 0) {
            plan.MissingArtifacts.Add(new PlatformInstallPlanArtifactStatus(identity, platformId));
            return;
        }

        InstalledArtifact installedArtifact = InstalledArtifacts[installedArtifactIndex];
        if (!ArtifactPathExists(installedArtifact.InstallPath)) {
            plan.BlockingIssues.Add(
                $"Installed artifact '{identity}' points to missing path '{installedArtifact.InstallPath}'.");
            return;
        }

        plan.ReusableArtifacts.Add(new PlatformInstallPlanArtifactStatus(identity, platformId, installedArtifact.InstallPath));
    }

    /// <summary>
    /// Finds the zero-based installed shared artifact index for the supplied exact reusable identity.
    /// </summary>
    /// <param name="identity">Exact shared artifact identity to locate.</param>
    /// <returns>Matching installed shared artifact index or <c>-1</c> when no exact reusable entry exists.</returns>
    int FindInstalledArtifactIndex(ArtifactIdentity identity) {
        for (int index = 0; index < InstalledArtifacts.Count; index++) {
            if (InstalledArtifacts[index].Identity.Equals(identity)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Determines whether the materialized artifact path currently exists on disk.
    /// </summary>
    /// <param name="installPath">Absolute artifact install path recorded in the local manifest.</param>
    /// <returns><c>true</c> when the artifact path exists as a directory or file; otherwise <c>false</c>.</returns>
    bool ArtifactPathExists(string installPath) {
        return Directory.Exists(installPath) || File.Exists(installPath);
    }
}
