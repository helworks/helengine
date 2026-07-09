using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city Nintendo DS build configuration includes every demo-disc physics scene exposed by the handheld menu.
/// </summary>
public sealed class CityNintendoDsBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city Nintendo DS build configuration includes the currently selectable physics scenes so handheld menu selection never targets an unbuilt scene.
    /// </summary>
    [Fact]
    public void City_nintendo_ds_build_config_includes_demo_disc_selectable_physics_scenes() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement dsPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "ds", StringComparison.Ordinal));
        JsonElement selectedSceneIds = dsPlatform.GetProperty("selectedSceneIds");
        JsonElement sceneOrders = dsPlatform.GetProperty("sceneOrders");
        string[] requiredSceneIds = [
            "test_scene_dynamic_stack_boxes",
            "test_scene_dynamic_sphere_stack",
            "test_scene_dynamic_mixed_stack",
            "test_scene_static_mesh_showcase",
            "test_scene_static_mesh_minimal"
        ];

        foreach (string requiredSceneId in requiredSceneIds) {
            Assert.Contains(selectedSceneIds.EnumerateArray(), sceneId => string.Equals(sceneId.GetString(), requiredSceneId, StringComparison.Ordinal));
            Assert.Contains(sceneOrders.EnumerateArray(), sceneOrder => string.Equals(sceneOrder.GetProperty("sceneId").GetString(), requiredSceneId, StringComparison.Ordinal));
        }
    }
}
