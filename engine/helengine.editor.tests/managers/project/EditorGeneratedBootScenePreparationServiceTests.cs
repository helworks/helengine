namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies generated boot-scene preparation writes the minimal SceneMapComponent helper scene for supported platforms.
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
    /// Ensures Nintendo DS boot-scene preparation writes one minimal helper scene containing a scene-map component.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsMenuSceneIsSelected_WritesBootSceneWithSceneMapComponent() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared("ds", [PlatformMenuSceneResolver.NintendoDsMainMenuSceneId]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.True(sceneAsset.SceneSettings.DontUnload);
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
        Assert.Equal("GeneratedBootSceneRoot", rootEntity.Name);
        Assert.Single(rootEntity.Components);

        SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
        SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal(PlatformMenuSceneResolver.NintendoDsMainMenuSceneId, sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
    }

    /// <summary>
    /// Ensures Nintendo DS boot-scene preparation also emits remaps for generated city rendering showcase companion scenes.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsRenderingShowcaseScenesAreSelected_WritesBootSceneWithRenderingCompanionMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.NintendoDsMainMenuSceneId,
                "cube_test",
                "cube_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
        SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
        SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));

        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
    }

    /// <summary>
    /// Ensures Windows boot-scene preparation writes the helper scene without any scene-id remapping entries.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenPlatformIsWindows_WritesBootSceneWithoutMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared("windows", [PlatformMenuSceneResolver.DesktopMainMenuSceneId]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.True(sceneAsset.SceneSettings.DontUnload);
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
        Assert.Single(rootEntity.Components);

        SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
        SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Empty(sceneMapComponent.Mappings);
    }

    /// <summary>
    /// Ensures unsupported platforms remain opt-in and do not write one generated boot scene.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenPlatformIsUnsupported_DoesNotWriteBootScene() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared("ps2", [PlatformMenuSceneResolver.DesktopMainMenuSceneId]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.False(File.Exists(scenePath));
    }
}
