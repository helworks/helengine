using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime startup manifest preserves injected startup metadata without relying on file parsing.
/// </summary>
public sealed class RuntimeStartupManifestTests {
    /// <summary>
    /// Ensures the constructor preserves the selected startup scene and storage profile.
    /// </summary>
    [Fact]
    public void Constructor_preserves_startup_metadata() {
        RuntimeStartupManifest manifest = new RuntimeStartupManifest(
            "Scenes/MainMenu.helen",
            new RuntimeStorageProfileId("windows-loose-files"));

        Assert.Equal("Scenes/MainMenu.helen", manifest.StartupSceneId);
        Assert.Equal("windows-loose-files", manifest.StorageProfileId.Value);
    }
}
