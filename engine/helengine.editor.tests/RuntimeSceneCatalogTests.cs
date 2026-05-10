using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene catalog preserves lookup behavior without relying on file parsing.
/// </summary>
public sealed class RuntimeSceneCatalogTests {
    /// <summary>
    /// Ensures the runtime scene catalog preserves scene ids and cooked relative paths.
    /// </summary>
    [Fact]
    public void Constructor_preserves_runtime_scene_catalog_shape() {
        RuntimeSceneCatalog catalog = new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")
            ]);

        Assert.Equal(2, catalog.Entries.Length);
        Assert.Equal("Scenes/Bootstrap.helen", catalog.Entries[0].SceneId);
        Assert.Equal("cooked/scenes/Bootstrap.hasset", catalog.Entries[0].CookedRelativePath);
        Assert.True(catalog.TryGetEntry("Scenes/TestPlayableScene.helen", out RuntimeSceneCatalogEntry entry));
        Assert.Equal("cooked/scenes/TestPlayableScene.hasset", entry.CookedRelativePath);
    }

    /// <summary>
    /// Ensures duplicate scene ids are rejected during catalog construction.
    /// </summary>
    [Fact]
    public void Constructor_whenSceneIdsAreDuplicated_throws() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene-copy.hasset")
            ]));

        Assert.Contains("Scenes/TestPlayableScene.helen", exception.Message);
    }

    /// <summary>
    /// Ensures cooked scene paths are normalized to forward slashes for runtime lookups and native exports.
    /// </summary>
    [Fact]
    public void Constructor_whenCookedRelativePathUsesBackslashes_normalizesToForwardSlashes() {
        RuntimeSceneCatalogEntry entry = new RuntimeSceneCatalogEntry(
            "Scenes/TestPlayableScene.helen",
            @"cooked\scenes\TestPlayableScene.hasset");

        Assert.Equal("cooked/scenes/TestPlayableScene.hasset", entry.CookedRelativePath);
    }
}
