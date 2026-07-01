using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city PSP build configuration includes every scene exposed by the demo-disc physics menu.
/// </summary>
public sealed class CityPspBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city PSP build configuration includes the currently selectable physics scenes so menu selection never targets an unbuilt scene.
    /// </summary>
    [Fact]
    public void City_psp_build_config_includes_demo_disc_selectable_physics_scenes() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement pspPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "psp", StringComparison.Ordinal));
        JsonElement selectedSceneIds = pspPlatform.GetProperty("selectedSceneIds");
        JsonElement sceneOrders = pspPlatform.GetProperty("sceneOrders");
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
