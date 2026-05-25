namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies generated boot-scene preparation writes the scene-map helper used to route Nintendo DS builds to authored companion scenes.
/// </summary>
public sealed class EditorGeneratedBootScenePreparationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project root for generated boot-scene preparation tests.
    /// </summary>
    public EditorGeneratedBootScenePreparationServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-boot-scene-preparation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
    }

    /// <summary>
    /// Deletes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures Nintendo DS build preparation rewrites the generated boot scene with scene-map entries for authored DS companion scenes.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsBuildIncludesCompanionScenes_WritesSceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.DesktopMainMenuSceneId,
                "cube_test",
                "cube_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.True(sceneAsset.SceneSettings.DontUnload);
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
        Assert.Equal("GeneratedBootSceneRoot", rootEntity.Name);

        SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
        SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
        Assert.False(sceneMapComponent.Mappings.ContainsKey(PlatformMenuSceneResolver.DesktopMainMenuSceneId));
    }

    /// <summary>
    /// Ensures Nintendo DS build preparation can derive logical scene-id mappings from a DS-only selected scene set.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsBuildIncludesOnlyDsScenes_WritesSceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                "DemoDiscMainMenuDs",
                "cube_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);

        SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
        SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal("DemoDiscMainMenuDs", sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
    }
}
