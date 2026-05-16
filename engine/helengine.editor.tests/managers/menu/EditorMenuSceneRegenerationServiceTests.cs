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
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.DemoMenuBuildComponent", serializedContents, StringComparison.Ordinal);
        Assert.DoesNotContain("helengine.ReferenceCanvasFitComponent, helengine.core", serializedContents, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures regenerating the Nintendo DS menu scene writes the dual-screen camera structure and viewport-backed menu root.
    /// </summary>
    [Fact]
    public void Regenerate_WhenInvokedForNintendoDs_WritesDualScreenMenuScene() {
        ScriptTypeResolver resolver = new ScriptTypeResolver();
        resolver.Register("gameplay", typeof(TestMenuDefinitionProvider).Assembly);
        EditorMenuSceneRegenerationService service = new EditorMenuSceneRegenerationService(ProjectRootPath, resolver);

        service.Regenerate("Scenes/DemoDiscMainMenuDs.helen", typeof(TestMenuDefinitionProvider).FullName + ", gameplay");

        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        using FileStream stream = File.OpenRead(scenePath);
        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        Assert.Collection(
            sceneAsset.RootEntities,
            entity => Assert.Equal("DemoDiscTopScreenCamera", entity.Name),
            entity => Assert.Equal("DemoDiscBottomScreenCamera", entity.Name));
        SceneEntityAsset topCameraEntity = sceneAsset.RootEntities[0];
        SceneEntityAsset bottomCameraEntity = sceneAsset.RootEntities[1];
        Assert.Contains(topCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");
        Assert.Contains(bottomCameraEntity.Components, component => component.ComponentTypeId == "helengine.CameraComponent");

        SceneEntityAsset topRootEntity = Assert.Single(topCameraEntity.Children, entity => entity.Name == "DemoDiscTopScreenRoot");
        Assert.Contains(topRootEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");
        SceneEntityAsset menuEntity = Assert.Single(bottomCameraEntity.Children, entity => entity.Name == "DemoDiscMenuRoot");
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == MenuComponent.SerializedComponentTypeId);
        Assert.Contains(menuEntity.Components, component => component.ComponentTypeId == "helengine.ViewportComponent, helengine.core");

        string serializedContents = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(scenePath));
        Assert.DoesNotContain("helengine.ReferenceCanvasFitComponent, helengine.core", serializedContents, StringComparison.Ordinal);
    }
}
