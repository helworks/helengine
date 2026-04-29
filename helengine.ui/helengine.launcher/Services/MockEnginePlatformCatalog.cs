using System.Collections.Generic;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services;

/// <summary>
/// Provides a hard-coded engine-platform catalog used until the Sweet Square network source is integrated.
/// </summary>
public sealed class MockEnginePlatformCatalog : IEnginePlatformCatalog {
    /// <summary>
    /// Stores the mocked catalog entries exposed to the launcher.
    /// </summary>
    IReadOnlyList<EngineCatalogEntry> Entries { get; } = new[] {
        new EngineCatalogEntry(
            "1.2.3",
            new[] {
                new EnginePlatformRequirement(
                    "android",
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformBuilder, "android-builder", "1.2.3")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformFiles, "android-platform-files", "1.2.3"))),
                new EnginePlatformRequirement(
                    "windows",
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.Sdk, "windows-sdk", "10.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformBuilder, "windows-builder", "1.2.3")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformFiles, "windows-platform-files", "1.2.3")))
            }),
        new EngineCatalogEntry(
            "2.0.0",
            new[] {
                new EnginePlatformRequirement(
                    "android",
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.Sdk, "android-sdk", "34.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformBuilder, "android-builder", "2.0.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformFiles, "android-platform-files", "2.0.0"))),
                new EnginePlatformRequirement(
                    "linux",
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.Sdk, "linux-sdk", "1.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformBuilder, "linux-builder", "2.0.0")),
                    new CatalogArtifactRequirement(new ArtifactIdentity(PlatformArtifactKind.PlatformFiles, "linux-platform-files", "2.0.0")))
            })
    };

    /// <summary>
    /// Gets the installable engine versions currently available to the launcher.
    /// </summary>
    /// <returns>Mocked catalog entries for launcher planning and install tests.</returns>
    public IReadOnlyList<EngineCatalogEntry> GetAvailableEngines() {
        return Entries;
    }
}
