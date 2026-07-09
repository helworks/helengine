namespace helengine.editor.tests;

/// <summary>
/// Verifies the city Nintendo DS headless build queue preserves the handheld physics scenes selected in persisted build settings.
/// </summary>
public sealed class CityNintendoDsBuildQueueItemTests {
    /// <summary>
    /// Ensures the queued Nintendo DS build item carries the currently selectable physics scenes into the shared build graph.
    /// </summary>
    [Fact]
    public void City_nintendo_ds_build_queue_item_includes_demo_disc_selectable_physics_scenes() {
        EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(@"C:\dev\helprojs\city\project.heproj");
        EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting();
        EditorBuildPlatformConfigDocument platformConfig = Assert.Single(
            buildConfig.Platforms,
            platform => string.Equals(platform.PlatformId, "ds", StringComparison.Ordinal));
        EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel("ds");

        EditorBuildQueueItemDocument queueItem = EditorBuildQueueItemDocument.Create(
            bootstrap.SceneCatalogService,
            platformConfig,
            selectionModel,
            @"C:\dev\helprojs\output\ds");

        Assert.Contains(queueItem.SelectedSceneIds, sceneId => string.Equals(sceneId, "test_scene_dynamic_stack_boxes", StringComparison.Ordinal));
        Assert.Contains(queueItem.SelectedSceneIds, sceneId => string.Equals(sceneId, "test_scene_dynamic_sphere_stack", StringComparison.Ordinal));
        Assert.Contains(queueItem.SelectedSceneIds, sceneId => string.Equals(sceneId, "test_scene_dynamic_mixed_stack", StringComparison.Ordinal));
        Assert.Contains(queueItem.SelectedSceneIds, sceneId => string.Equals(sceneId, "test_scene_static_mesh_showcase", StringComparison.Ordinal));
        Assert.Contains(queueItem.SelectedSceneIds, sceneId => string.Equals(sceneId, "test_scene_static_mesh_minimal", StringComparison.Ordinal));
    }
}
