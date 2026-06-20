namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies generated boot-scene preparation no longer injects platform-specific scene remapping.
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
    /// Ensures generated boot-scene preparation writes an empty shared scene-map when the generated boot scene is selected.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenBuildIncludesGeneratedBootScene_WritesEmptySceneMapMappings() {
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

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Empty(sceneMapComponent.Mappings);
    }

    /// <summary>
    /// Ensures generated boot-scene preparation ignores platform-specific scene-id naming conventions.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenBuildIncludesOnlyPlatformSpecificSceneNames_WritesEmptySceneMapMappings() {
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

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Empty(sceneMapComponent.Mappings);
    }

    /// <summary>
    /// Ensures platform id no longer changes generated boot-scene mappings.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendo3DsBuildIncludesOnlyDsScenes_WritesEmptySceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "3ds",
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

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Empty(sceneMapComponent.Mappings);
    }

    /// <summary>
    /// Deserializes one generated boot-scene scene-map record through the shared automatic component descriptor.
    /// </summary>
    /// <param name="record">Serialized generated boot-scene component record.</param>
    /// <returns>Deserialized scene-map component.</returns>
    static SceneMapComponent DeserializeSceneMapComponent(SceneComponentAssetRecord record) {
        AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
        return Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(record, null, null));
    }
}
