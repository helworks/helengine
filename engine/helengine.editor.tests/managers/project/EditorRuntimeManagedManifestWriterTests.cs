using System.Collections.Generic;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the managed runtime manifest writer emits the scene catalog JSON file.
/// </summary>
public sealed class EditorRuntimeManagedManifestWriterTests : IDisposable {
    /// <summary>
    /// Temporary workspace used by the test.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Initializes the temporary workspace.
    /// </summary>
    public EditorRuntimeManagedManifestWriterTests() {
        RootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-managed-manifest-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>
    /// Deletes the temporary workspace.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Ensures the runtime scene catalog file is written for managed players.
    /// </summary>
    [Fact]
    public void Write_emits_runtime_scene_catalog_json_file() {
        PlatformBuildManifest manifest = new(
            2,
            "project",
            "1.0.0",
            "1.0.0",
            "Scenes/Bootstrap.helen",
            [
                new PlatformBuildScene(
                    "Scenes/Bootstrap.helen",
                    "Bootstrap",
                    "Scenes/Bootstrap.helen",
                    Array.Empty<PlatformBuildPayloadReference>(),
                    [
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/Bootstrap.hasset")
                    ]),
                new PlatformBuildScene(
                    "Scenes/TestPlayableScene.helen",
                    "TestPlayableScene",
                    "Scenes/TestPlayableScene.helen",
                    Array.Empty<PlatformBuildPayloadReference>(),
                    [
                        new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/TestPlayableScene.hasset")
                    ])
            ],
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

        EditorRuntimeManagedManifestWriter writer = new();
        writer.Write(RootPath, manifest);

        string startupPath = Path.Combine(RootPath, "runtime-startup.json");
        string sceneCatalogPath = Path.Combine(RootPath, "runtime-scene-catalog.json");

        Assert.False(File.Exists(startupPath));
        Assert.True(File.Exists(sceneCatalogPath));

        string sceneCatalogJson = File.ReadAllText(sceneCatalogPath);

        Assert.Contains("\"SceneId\": \"Scenes/TestPlayableScene.helen\"", sceneCatalogJson);
        Assert.Contains("\"CookedRelativePath\": \"cooked/scenes/TestPlayableScene.hasset\"", sceneCatalogJson);
    }
}
