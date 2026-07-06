using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city Windows build configuration packages the boot scene, demo-disc menu, and every scene reachable from it.
/// </summary>
public sealed class CityWindowsBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city Windows build configuration keeps the generated boot scene first, routes into <c>DemoDiscMainMenu</c>, and includes every currently selectable demo-disc scene.
    /// </summary>
    [Fact]
    public void City_windows_build_config_includes_generated_boot_scene_and_demo_disc_scene_set() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement windowsPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "windows", StringComparison.Ordinal));
        JsonElement selectedSceneIds = windowsPlatform.GetProperty("selectedSceneIds");
        JsonElement sceneOrders = windowsPlatform.GetProperty("sceneOrders");
        string[] requiredSceneIds = [
            "GeneratedBootScene",
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

        Assert.Equal("GeneratedBootScene", selectedSceneIds[0].GetString());
        Assert.Equal("DemoDiscMainMenu", selectedSceneIds[1].GetString());
        JsonElement firstSceneOrder = sceneOrders.EnumerateArray().Single(sceneOrder => sceneOrder.GetProperty("orderNumber").GetInt32() == 1);
        JsonElement secondSceneOrder = sceneOrders.EnumerateArray().Single(sceneOrder => sceneOrder.GetProperty("orderNumber").GetInt32() == 2);
        Assert.Equal("GeneratedBootScene", firstSceneOrder.GetProperty("sceneId").GetString());
        Assert.Equal("DemoDiscMainMenu", secondSceneOrder.GetProperty("sceneId").GetString());
        Assert.Equal(requiredSceneIds.Length, selectedSceneIds.GetArrayLength());
        Assert.Equal(requiredSceneIds.Length, sceneOrders.GetArrayLength());

        for (int index = 0; index < requiredSceneIds.Length; index++) {
            Assert.Equal(requiredSceneIds[index], selectedSceneIds[index].GetString());
            JsonElement orderedScene = sceneOrders.EnumerateArray().Single(sceneOrder => sceneOrder.GetProperty("orderNumber").GetInt32() == index + 1);
            Assert.Equal(requiredSceneIds[index], orderedScene.GetProperty("sceneId").GetString());
        }
    }
}
