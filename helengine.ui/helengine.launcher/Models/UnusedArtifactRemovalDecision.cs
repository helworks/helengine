using System.Collections.Generic;
using System.Linq;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes the unused shared artifacts that the launcher can offer to remove after uninstalling an engine.
/// </summary>
public sealed class UnusedArtifactRemovalDecision {
    /// <summary>
    /// Initializes one unused-artifact removal decision.
    /// </summary>
    /// <param name="engineVersion">Engine version whose removal produced the unused artifacts.</param>
    /// <param name="unusedArtifacts">Shared artifacts no longer referenced by any installed engine.</param>
    public UnusedArtifactRemovalDecision(string engineVersion, IEnumerable<InstalledArtifact> unusedArtifacts) {
        EngineVersion = engineVersion;
        UnusedArtifacts = unusedArtifacts.ToArray();
    }

    /// <summary>
    /// Gets the engine version whose removal produced the unused shared artifacts.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the shared artifacts no longer referenced by any installed engine.
    /// </summary>
    public IReadOnlyList<InstalledArtifact> UnusedArtifacts { get; }
}
