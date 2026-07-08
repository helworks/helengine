using System.Text.Json;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city DS platform configuration keeps runtime diagnostics console output disabled so the bottom screen remains available for scene UI.
/// </summary>
public sealed class CityDsBuildConfigSourceTests {
    /// <summary>
    /// Ensures the persisted city DS platform settings do not opt into native runtime diagnostics.
    /// </summary>
    [Fact]
    public void City_ds_platform_settings_disable_native_runtime_diagnostics() {
        string sourcePath = @"C:\dev\helprojs\city\settings\platform.ds.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement selectedOptionValues = document.RootElement.GetProperty("build").GetProperty("selectedOptionValues");

        bool hasDiagnosticsValue = selectedOptionValues.TryGetProperty("enable-native-runtime-diagnostics", out JsonElement diagnosticsValue);

        if (!hasDiagnosticsValue) {
            return;
        }

        Assert.NotEqual("true", diagnosticsValue.GetString());
    }

    /// <summary>
    /// Ensures the persisted city DS build configuration does not override the packaged runtime back into native diagnostics mode.
    /// </summary>
    [Fact]
    public void City_ds_build_config_does_not_override_native_runtime_diagnostics_on() {
        string sourcePath = @"C:\dev\helprojs\city\user_settings\build_config.json";
        string source = File.ReadAllText(sourcePath);
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement platforms = document.RootElement.GetProperty("platforms");
        JsonElement dsPlatform = platforms.EnumerateArray().Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "ds", StringComparison.Ordinal));
        JsonElement selectedBuildOptionValues = dsPlatform.GetProperty("selectedBuildOptionValues");

        bool hasDiagnosticsValue = selectedBuildOptionValues.TryGetProperty("enable-native-runtime-diagnostics", out JsonElement diagnosticsValue);

        if (!hasDiagnosticsValue) {
            return;
        }

        Assert.NotEqual("true", diagnosticsValue.GetString());
    }
}
