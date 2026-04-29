using System.Collections.Generic;
using System.Linq;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Describes one user-selected engine version and the platforms chosen for planning or install.
/// </summary>
public sealed class PlatformInstallSelection {
    /// <summary>
    /// Initializes one platform install selection.
    /// </summary>
    /// <param name="engineVersion">Exact engine version selected for installation.</param>
    /// <param name="platformIds">Stable platform identifiers chosen by the user.</param>
    public PlatformInstallSelection(string engineVersion, IEnumerable<string> platformIds) {
        EngineVersion = engineVersion;
        PlatformIds = platformIds.ToArray();
    }

    /// <summary>
    /// Gets the exact engine version selected for installation.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the stable platform identifiers chosen by the user.
    /// </summary>
    public IReadOnlyList<string> PlatformIds { get; }
}
