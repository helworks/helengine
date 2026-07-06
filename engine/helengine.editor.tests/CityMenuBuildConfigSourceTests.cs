namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city demo-disc menu generation flow derives per-platform scene availability from build configuration at authoring time instead of filtering the menu only after boot.
/// </summary>
public sealed class CityMenuBuildConfigSourceTests {
    /// <summary>
    /// Ensures the demo-disc scene generator routes the canonical menu definition through the build-scene authoring service before persisting the generated scene.
    /// </summary>
    [Fact]
    public void City_demo_disc_menu_generator_source_applies_build_scene_authoring_service_before_writing_scene() {
        string generatorSourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscSceneGenerator.cs";
        string generatorSource = File.ReadAllText(generatorSourcePath);
        string authoringServiceSourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMenuBuildSceneAuthoringService.cs";
        string authoringServiceSource = File.ReadAllText(authoringServiceSourcePath);

        Assert.Contains("readonly DemoDiscMenuBuildSceneAuthoringService MenuBuildSceneAuthoringService;", generatorSource, StringComparison.Ordinal);
        Assert.Contains("MenuBuildSceneAuthoringService = new DemoDiscMenuBuildSceneAuthoringService();", generatorSource, StringComparison.Ordinal);
        Assert.Contains("MenuBuildSceneAuthoringService.ApplyBuildSceneAvailability(projectRootPath, sceneDefinition, definition);", generatorSource, StringComparison.Ordinal);
        Assert.Contains("new EditorBuildConfigService(projectRootPath).TryLoadExisting()", authoringServiceSource, StringComparison.Ordinal);
        Assert.Contains("PlatformSceneAuthoringHelperService.ExcludeEntitySubtreeFromPlatforms", authoringServiceSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the runtime menu definition provider once again emits the canonical full menu catalog without consulting the packaged runtime scene catalog.
    /// </summary>
    [Fact]
    public void City_demo_disc_menu_provider_source_no_longer_runtime_filters_scene_items() {
        string providerSourcePath = @"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMenuDefinitionProvider.cs";
        string providerSource = File.ReadAllText(providerSourcePath);

        Assert.DoesNotContain("ResolveRuntimeSceneCatalog", providerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilterUnavailableSceneItems", providerSource, StringComparison.Ordinal);
        Assert.Contains("\"main-physics\", \"Physics Scenes\"", providerSource, StringComparison.Ordinal);
        Assert.Contains("\"physics-select\",", providerSource, StringComparison.Ordinal);
        Assert.Contains("sceneCatalog.CreatePhysicsSceneItems()", providerSource, StringComparison.Ordinal);
    }
}
