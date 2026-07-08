namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies generated boot-scene preparation no longer injects platform-specific scene remapping.
/// </summary>
public sealed class EditorGeneratedBootScenePreparationServiceTests : IDisposable {
    /// <summary>
    /// Environment variable used to override the generated boot-scene startup target during build preparation.
    /// </summary>
    const string GeneratedBootSceneInitialSceneIdEnvironmentVariable = "HELENGINE_GENERATED_BOOT_SCENE_INITIAL_SCENE_ID";

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
        Environment.SetEnvironmentVariable(GeneratedBootSceneInitialSceneIdEnvironmentVariable, null);
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures desktop boot-scene preparation can direct startup into an explicitly selected scene for fast runtime repro.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenDesktopStartupOverrideIsSet_WritesOverrideAsInitialSceneId() {
        Environment.SetEnvironmentVariable(GeneratedBootSceneInitialSceneIdEnvironmentVariable, "test_scene_static_mesh_showcase");
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "windows",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.DesktopMainMenuSceneId,
                "test_scene_static_mesh_showcase"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal("test_scene_static_mesh_showcase", sceneMapComponent.InitialSceneId);
        Assert.Empty(sceneMapComponent.Mappings);
    }

    /// <summary>
    /// Ensures boot-scene preparation rejects startup overrides that are not present in the selected scene set.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenStartupOverrideTargetsUnselectedScene_Throws() {
        Environment.SetEnvironmentVariable(GeneratedBootSceneInitialSceneIdEnvironmentVariable, "test_scene_static_mesh_showcase");
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.EnsurePrepared(
            "windows",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.DesktopMainMenuSceneId
            ]));

        Assert.Contains("test_scene_static_mesh_showcase", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures generated boot-scene preparation writes Nintendo DS remaps for the generated boot scene.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenBuildIncludesGeneratedBootScene_WritesNintendoDsSceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
                "cube_test_ds",
                "axis_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.True(sceneAsset.SceneSettings.DontUnload);
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
        Assert.Equal("GeneratedBootSceneRoot", rootEntity.Name);

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal(
            PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
            sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
        Assert.Equal("axis_test_ds", sceneMapComponent.Mappings["axis_test"]);
    }

    /// <summary>
    /// Ensures generated boot-scene preparation derives remaps from Nintendo DS companion-scene ids.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenBuildIncludesOnlyPlatformSpecificSceneNames_WritesNintendoDsSceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
                "cube_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal(
            PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
            sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
    }

    /// <summary>
    /// Ensures Nintendo 3DS boot-scene preparation reuses the Nintendo DS companion-scene remap behavior.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendo3DsBuildIncludesOnlyDsScenes_WritesNintendoDsSceneMapMappings() {
        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "3ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
                "cube_test_ds"
            ]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        Assert.True(File.Exists(scenePath));

        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal(
            PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
            sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
    }

    /// <summary>
    /// Ensures generated boot-scene preparation refreshes stale generated boot-scene assets with the current Nintendo DS remap set.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenGeneratedBootSceneAlreadyExists_RewritesMappingsFromNintendoDsSceneSelection() {
        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
        GeneratedBootSceneAssetFactory factory = new GeneratedBootSceneAssetFactory();
        SceneAsset existingSceneAsset = factory.BuildSceneAsset(
            "Scenes/" + PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen",
            PlatformMenuSceneResolver.DesktopMainMenuSceneId,
            new Dictionary<string, string>(StringComparer.Ordinal));
        using (FileStream stream = File.Create(scenePath)) {
            AssetSerializer.Serialize(stream, existingSceneAsset);
        }

        EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

        service.EnsurePrepared(
            "ds",
            [
                PlatformMenuSceneResolver.GeneratedBootSceneId,
                PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
                "cube_test_ds",
                "axis_test_ds"
            ]);

        using FileStream readStream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(readStream));
        SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);

        SceneMapComponent sceneMapComponent = DeserializeSceneMapComponent(rootEntity.Components[0]);
        Assert.Equal(PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId, sceneMapComponent.InitialSceneId);
        Assert.Equal(3, sceneMapComponent.Mappings.Count);
        Assert.Equal(
            PlatformMenuSceneResolver.NintendoHandheldMainMenuSceneId,
            sceneMapComponent.Mappings[PlatformMenuSceneResolver.DesktopMainMenuSceneId]);
        Assert.Equal("cube_test_ds", sceneMapComponent.Mappings["cube_test"]);
        Assert.Equal("axis_test_ds", sceneMapComponent.Mappings["axis_test"]);
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
