using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.menu;

/// <summary>
/// Verifies the editor-side menu scene regeneration service rewrites baked menu scenes through the normal serializer.
/// </summary>
public sealed class EditorMenuSceneRegenerationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project root for scene regeneration tests.
    /// </summary>
    public EditorMenuSceneRegenerationServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-menu-scene-regeneration-tests", Guid.NewGuid().ToString("N"));
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
    /// Ensures regenerating one menu scene writes current generic menu component ids.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvoked_WritesMenuComponentTypeIds() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenu.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenu.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
        SceneComponentAssetRecord menuRecord = Assert.Single(menuEntity.Components);
        Assert.Equal(MenuComponent.SerializedComponentTypeId, menuRecord.ComponentTypeId);
        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.DemoMenuBuildComponent", serializedContents, StringComparison.Ordinal);
    }
}
