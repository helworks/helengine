using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city PS Vita build configuration persists the expected startup-scene ordering.
/// </summary>
public sealed class CityPsVitaBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city PS Vita build configuration promotes <c>cube_test</c> to the first startup-scene slot.
    /// </summary>
    [Fact]
    public void City_psvita_build_config_uses_cube_test_as_first_scene() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement psVitaPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "psvita", StringComparison.Ordinal));
        JsonElement selectedSceneIds = psVitaPlatform.GetProperty("selectedSceneIds");
        JsonElement sceneOrders = psVitaPlatform.GetProperty("sceneOrders");

        Assert.Equal("cube_test", selectedSceneIds[0].GetString());
        JsonElement firstSceneOrder = sceneOrders.EnumerateArray().Single(sceneOrder => sceneOrder.GetProperty("orderNumber").GetInt32() == 1);
        Assert.Equal("cube_test", firstSceneOrder.GetProperty("sceneId").GetString());
    }

    /// <summary>
    /// Ensures the persisted city PS Vita build configuration supplies the generic numeric type remaps required by native code generation.
    /// </summary>
    [Fact]
    public void City_psvita_build_config_includes_generic_numeric_type_remaps() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement psVitaPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "psvita", StringComparison.Ordinal));
        JsonElement selectedCodegenOptionValues = psVitaPlatform.GetProperty("selectedCodegenOptionValues");
        string typeRemaps = selectedCodegenOptionValues.GetProperty("type-remaps").GetString();

        Assert.Equal("System.Numerics.Vector2=helengine.float2;System.Numerics.Vector3=helengine.float3;System.Numerics.Vector4=helengine.float4;System.Numerics.Quaternion=helengine.float4", typeRemaps);
    }

    /// <summary>
    /// Ensures the persisted city PS Vita build configuration supplies the generic native C++ platform-shape options required by custom codegen platforms.
    /// </summary>
    [Fact]
    public void City_psvita_build_config_includes_required_custom_cpp_platform_shape_options() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement psVitaPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "psvita", StringComparison.Ordinal));
        JsonElement selectedCodegenOptionValues = psVitaPlatform.GetProperty("selectedCodegenOptionValues");

        Assert.Equal("native-column-vector", selectedCodegenOptionValues.GetProperty("generated-math-convention").GetString());
        Assert.Equal("4", selectedCodegenOptionValues.GetProperty("pointer-size-bytes").GetString());
    }
}
