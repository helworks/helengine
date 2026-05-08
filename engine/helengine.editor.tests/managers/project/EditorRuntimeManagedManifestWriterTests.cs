using System.Collections.Generic;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the managed runtime manifest writer emits startup and scene catalog JSON files.
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
    /// Ensures startup and runtime scene catalog files are written for managed players.
    /// </summary>
    [Fact]
    public void Write_emits_runtime_startup_and_scene_catalog_json_files() {
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
        writer.Write(RootPath, manifest, "windows-loose-files");

        string startupPath = Path.Combine(RootPath, "runtime-startup.json");
        string sceneCatalogPath = Path.Combine(RootPath, "runtime-scene-catalog.json");

        Assert.True(File.Exists(startupPath));
        Assert.True(File.Exists(sceneCatalogPath));

        string startupJson = File.ReadAllText(startupPath);
        string sceneCatalogJson = File.ReadAllText(sceneCatalogPath);

        Assert.Contains("\"StartupSceneId\": \"Scenes/Bootstrap.helen\"", startupJson);
        Assert.Contains("\"Value\": \"windows-loose-files\"", startupJson);
        Assert.Contains("\"SceneId\": \"Scenes/TestPlayableScene.helen\"", sceneCatalogJson);
        Assert.Contains("\"CookedRelativePath\": \"cooked/scenes/TestPlayableScene.hasset\"", sceneCatalogJson);
    }
}
