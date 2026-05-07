using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the runtime scene catalog reader matches the editor-written JSON shape.
/// </summary>
public sealed class RuntimeSceneCatalogTests : IDisposable {
    /// <summary>
    /// Temporary root used by the catalog tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes the temporary test root.
    /// </summary>
    public RuntimeSceneCatalogTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
    }

    /// <summary>
    /// Deletes the temporary test root.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the runtime scene catalog preserves scene ids and cooked relative paths.
    /// </summary>
    [Fact]
    public void ReadFromFile_parses_runtime_scene_catalog_shape() {
        string manifestPath = Path.Combine(TempRootPath, "runtime-scene-catalog.json");
        File.WriteAllText(
            manifestPath,
            """
            {
              "Entries": [
                {
                  "SceneId": "Scenes/Bootstrap.helen",
                  "CookedRelativePath": "cooked/scenes/main.hasset"
                },
                {
                  "SceneId": "Scenes/TestPlayableScene.helen",
                  "CookedRelativePath": "scenes/Scenes/TestPlayableScene.hasset"
                }
              ]
            }
            """);

        RuntimeSceneCatalog catalog = RuntimeSceneCatalog.ReadFromFile(manifestPath);

        Assert.Equal(2, catalog.Entries.Length);
        Assert.Equal("Scenes/Bootstrap.helen", catalog.Entries[0].SceneId);
        Assert.Equal("cooked/scenes/main.hasset", catalog.Entries[0].CookedRelativePath);
        Assert.True(catalog.TryGetEntry("Scenes/TestPlayableScene.helen", out RuntimeSceneCatalogEntry entry));
        Assert.Equal("scenes/Scenes/TestPlayableScene.hasset", entry.CookedRelativePath);
    }

    /// <summary>
    /// Ensures duplicate scene ids are rejected during catalog construction.
    /// </summary>
    [Fact]
    public void Constructor_whenSceneIdsAreDuplicated_throws() {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new RuntimeSceneCatalog(
            [
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene-copy.hasset")
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
            @"scenes\Scenes\TestPlayableScene.hasset");

        Assert.Equal("scenes/Scenes/TestPlayableScene.hasset", entry.CookedRelativePath);
    }
}
