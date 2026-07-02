using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city Wii U build configuration packages the demo-disc main menu and every scene reachable from it.
/// </summary>
public sealed class CityWiiUBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city Wii U build configuration keeps <c>DemoDiscMainMenu</c> first and includes every currently selectable demo-disc scene.
    /// </summary>
    [Fact]
    public void City_wiiu_build_config_includes_demo_disc_main_menu_scene_set() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement wiiuPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "wiiu", StringComparison.Ordinal));
        JsonElement selectedSceneIds = wiiuPlatform.GetProperty("selectedSceneIds");
        JsonElement sceneOrders = wiiuPlatform.GetProperty("sceneOrders");
        string[] requiredSceneIds = [
            "DemoDiscMainMenu",
            "cube_test",
            "colored_cube_grid",
            "textured_cube_grid",
            "axis_test",
            "axis_test2",
            "test_scene_matrix_render",
            "directional_shadow_plaza",
            "test_scene_dynamic_stack_boxes",
            "test_scene_dynamic_sphere_stack",
            "test_scene_dynamic_mixed_stack",
            "test_scene_static_mesh_showcase",
            "test_scene_static_mesh_minimal",
            "tilt_trial"
        ];

        Assert.Equal("DemoDiscMainMenu", selectedSceneIds[0].GetString());
        JsonElement firstSceneOrder = sceneOrders.EnumerateArray().Single(sceneOrder => sceneOrder.GetProperty("orderNumber").GetInt32() == 1);
        Assert.Equal("DemoDiscMainMenu", firstSceneOrder.GetProperty("sceneId").GetString());
        Assert.Equal(requiredSceneIds.Length, selectedSceneIds.GetArrayLength());
        Assert.Equal(requiredSceneIds.Length, sceneOrders.GetArrayLength());

        foreach (string requiredSceneId in requiredSceneIds) {
            Assert.Contains(selectedSceneIds.EnumerateArray(), sceneId => string.Equals(sceneId.GetString(), requiredSceneId, StringComparison.Ordinal));
            Assert.Contains(sceneOrders.EnumerateArray(), sceneOrder => string.Equals(sceneOrder.GetProperty("sceneId").GetString(), requiredSceneId, StringComparison.Ordinal));
        }
    }
}
