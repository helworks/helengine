using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies build-time preparation regenerates missing platform-owned menu scene assets before selected-scene resolution begins.
/// </summary>
public sealed class EditorGeneratedMenuScenePreparationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project root for generated menu-scene preparation tests.
    /// </summary>
    public EditorGeneratedMenuScenePreparationServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-menu-scene-preparation-tests", Guid.NewGuid().ToString("N"));
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
    /// Ensures Nintendo DS build preparation regenerates the missing DS menu scene from the desktop menu provider contract.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsMenuSceneIsMissing_RegeneratesItFromDesktopMenuProvider() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService regenerationService = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);
        regenerationService.Regenerate("Scenes/DemoDiscMainMenu.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        EditorGeneratedMenuScenePreparationService service = new EditorGeneratedMenuScenePreparationService(ProjectRootPath, resolver);

        service.EnsurePrepared([PlatformMenuSceneResolver.NintendoDsMainMenuSceneId]);

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        Assert.True(File.Exists(scenePath));
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
        Assert.Collection(
            sceneAsset.RootEntities,
            entity => Assert.Equal("DemoDiscTopScreenCamera", entity.Name),
            entity => Assert.Equal("DemoDiscBottomScreenCamera", entity.Name));
    }

    /// <summary>
    /// Ensures Nintendo DS build preparation fails clearly when the generated DS menu scene is missing and no script resolver is available.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsMenuSceneIsMissingAndNoScriptResolverExists_ThrowsClearError() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService regenerationService = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);
        regenerationService.Regenerate("Scenes/DemoDiscMainMenu.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        EditorGeneratedMenuScenePreparationService service = new EditorGeneratedMenuScenePreparationService(ProjectRootPath, null);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.EnsurePrepared([PlatformMenuSceneResolver.NintendoDsMainMenuSceneId]));

        Assert.Contains("DemoDiscMainMenuDs", exception.Message, StringComparison.Ordinal);
    }
}
