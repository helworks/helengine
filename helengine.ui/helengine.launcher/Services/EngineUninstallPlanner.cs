using System;
using System.Collections.Generic;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Computes which shared artifacts become unused after removing one installed engine version.
/// </summary>
public sealed class EngineUninstallPlanner {
    /// <summary>
    /// Stores the installed shared artifacts currently known to the launcher.
    /// </summary>
    IReadOnlyList<InstalledArtifact> InstalledArtifacts { get; }

    /// <summary>
    /// Stores the installed engine-platform bindings currently known to the launcher.
    /// </summary>
    IReadOnlyList<InstalledEnginePlatformBinding> InstalledBindings { get; }

    /// <summary>
    /// Creates one uninstall planner for the supplied installed artifacts and bindings.
    /// </summary>
    /// <param name="installedArtifacts">Installed shared artifacts currently tracked by the launcher.</param>
    /// <param name="installedBindings">Installed engine-platform bindings currently tracked by the launcher.</param>
    public EngineUninstallPlanner(IEnumerable<InstalledArtifact> installedArtifacts, IEnumerable<InstalledEnginePlatformBinding> installedBindings) {
        InstalledArtifacts = new List<InstalledArtifact>(installedArtifacts);
        InstalledBindings = new List<InstalledEnginePlatformBinding>(installedBindings);
    }

    /// <summary>
    /// Computes the shared artifacts that would become unreferenced after removing the supplied engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version being removed.</param>
    /// <returns>Unused shared artifacts that the launcher can offer to remove.</returns>
    public UnusedArtifactRemovalDecision GetUnusedArtifactsAfterRemoving(string engineVersion) {
        List<InstalledArtifact> unusedArtifacts = new();
        for (int index = 0; index < InstalledBindings.Count; index++) {
            InstalledEnginePlatformBinding binding = InstalledBindings[index];
            if (!string.Equals(binding.EngineVersion, engineVersion, StringComparison.Ordinal)) {
                continue;
            }

            AddUnusedArtifactIfNeeded(unusedArtifacts, binding.SdkIdentity, engineVersion);
            AddUnusedArtifactIfNeeded(unusedArtifacts, binding.PlatformBuilderIdentity, engineVersion);
            AddUnusedArtifactIfNeeded(unusedArtifacts, binding.PlatformFilesIdentity, engineVersion);
        }

        return new UnusedArtifactRemovalDecision(engineVersion, unusedArtifacts);
    }

    /// <summary>
    /// Adds one shared artifact to the unused set when the removed engine was its final reference.
    /// </summary>
    /// <param name="unusedArtifacts">Unused shared artifacts being accumulated.</param>
    /// <param name="identity">Exact reusable artifact identity being evaluated.</param>
    /// <param name="removedEngineVersion">Exact engine version being removed.</param>
    void AddUnusedArtifactIfNeeded(List<InstalledArtifact> unusedArtifacts, ArtifactIdentity identity, string removedEngineVersion) {
        if (HasRemainingReference(identity, removedEngineVersion)) {
            return;
        }

        int installedArtifactIndex = FindInstalledArtifactIndex(identity);
        if (installedArtifactIndex < 0) {
            return;
        }

        InstalledArtifact artifact = InstalledArtifacts[installedArtifactIndex];
        if (!ContainsArtifact(unusedArtifacts, artifact.Identity)) {
            unusedArtifacts.Add(artifact);
        }
    }

    /// <summary>
    /// Determines whether one shared artifact remains referenced by any engine other than the removed engine version.
    /// </summary>
    /// <param name="identity">Exact reusable artifact identity to evaluate.</param>
    /// <param name="removedEngineVersion">Exact engine version being removed.</param>
    /// <returns><c>true</c> when another installed engine still references the artifact; otherwise <c>false</c>.</returns>
    bool HasRemainingReference(ArtifactIdentity identity, string removedEngineVersion) {
        for (int index = 0; index < InstalledBindings.Count; index++) {
            InstalledEnginePlatformBinding binding = InstalledBindings[index];
            if (string.Equals(binding.EngineVersion, removedEngineVersion, StringComparison.Ordinal)) {
                continue;
            }

            if (binding.SdkIdentity.Equals(identity)
                || binding.PlatformBuilderIdentity.Equals(identity)
                || binding.PlatformFilesIdentity.Equals(identity)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the zero-based installed-artifact index for the supplied exact reusable identity.
    /// </summary>
    /// <param name="identity">Exact reusable artifact identity to match.</param>
    /// <returns>Matching installed-artifact index or <c>-1</c> when the artifact is not tracked.</returns>
    int FindInstalledArtifactIndex(ArtifactIdentity identity) {
        for (int index = 0; index < InstalledArtifacts.Count; index++) {
            if (InstalledArtifacts[index].Identity.Equals(identity)) {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Determines whether the supplied unused-artifact list already contains the exact reusable identity.
    /// </summary>
    /// <param name="unusedArtifacts">Unused shared artifacts already accumulated.</param>
    /// <param name="identity">Exact reusable identity to match.</param>
    /// <returns><c>true</c> when the identity is already present; otherwise <c>false</c>.</returns>
    static bool ContainsArtifact(IReadOnlyList<InstalledArtifact> unusedArtifacts, ArtifactIdentity identity) {
        for (int index = 0; index < unusedArtifacts.Count; index++) {
            if (unusedArtifacts[index].Identity.Equals(identity)) {
                return true;
            }
        }

        return false;
    }
}
