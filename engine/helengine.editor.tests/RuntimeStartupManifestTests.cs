using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime startup manifest preserves injected startup metadata without relying on file parsing.
/// </summary>
public sealed class RuntimeStartupManifestTests {
    /// <summary>
    /// Ensures the constructor preserves the selected startup scene, storage profile, and platform metadata.
    /// </summary>
    [Fact]
    public void Constructor_preserves_startup_metadata() {
        RuntimeStartupManifest manifest = new RuntimeStartupManifest(
            "Scenes/MainMenu.helen",
            new RuntimeStorageProfileId("windows-loose-files"),
            new PlatformInfo("windows", "2026.05.12"));

        Assert.Equal("Scenes/MainMenu.helen", manifest.StartupSceneId);
        Assert.Equal("windows-loose-files", manifest.StorageProfileId.Value);
        Assert.Equal("windows", manifest.PlatformInfo.Name);
        Assert.Equal("2026.05.12", manifest.PlatformInfo.Version);
    }
}
