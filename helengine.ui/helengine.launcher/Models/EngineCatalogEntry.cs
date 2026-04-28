using System.Collections.Generic;
using System.Linq;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one engine version and the installable platform requirements exposed for that version.
/// </summary>
public sealed class EngineCatalogEntry {
    /// <summary>
    /// Initializes one engine catalog entry.
    /// </summary>
    /// <param name="engineVersion">Exact installable engine version.</param>
    /// <param name="platformRequirements">Platform requirements available for the engine version.</param>
    public EngineCatalogEntry(string engineVersion, IEnumerable<EnginePlatformRequirement> platformRequirements) {
        EngineVersion = engineVersion;
        PlatformRequirements = platformRequirements.ToArray();
    }

    /// <summary>
    /// Gets the exact installable engine version.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the available platform requirements for this engine version.
    /// </summary>
    public IReadOnlyList<EnginePlatformRequirement> PlatformRequirements { get; }
}
