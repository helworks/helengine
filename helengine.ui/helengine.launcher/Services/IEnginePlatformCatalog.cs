using System.Collections.Generic;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Exposes installable engine versions and their per-platform shared artifact requirements.
/// </summary>
public interface IEnginePlatformCatalog {
    /// <summary>
    /// Gets the installable engine versions currently available to the launcher.
    /// </summary>
    /// <returns>Catalog entries that describe engine versions and their platform requirements.</returns>
    IReadOnlyList<EngineCatalogEntry> GetAvailableEngines();
}
