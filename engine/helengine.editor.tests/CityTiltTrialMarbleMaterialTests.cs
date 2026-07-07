namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Tilt Trial marble material keeps the Windows standard-shader material fields required by the Tilt Trial marble workflow.
/// </summary>
public sealed class CityTiltTrialMarbleMaterialTests {
    /// <summary>
    /// Absolute authored marble material path generated for the Tilt Trial player sphere.
    /// </summary>
    const string TiltTrialMarbleMaterialPath = @"C:\dev\helprojs\city\assets\materials\rendering\tilt_trial\PlayerSphereMarble.hasset";

    /// <summary>
    /// Ensures the authored Windows marble material preserves metallic, specular, roughness, and the imported roughness texture reference.
    /// </summary>
    [Fact]
    public void Tilt_trial_marble_material_source_preserves_windows_metallic_specular_and_roughness_fields() {
        MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();

        Assert.True(File.Exists(TiltTrialMarbleMaterialPath), $"Expected Tilt Trial marble material at '{TiltTrialMarbleMaterialPath}'.");
        Assert.True(settingsService.TryLoadPlatformSettings(TiltTrialMarbleMaterialPath, "windows", out MaterialAssetProcessorSettings platformSettings));
        Assert.NotNull(platformSettings);
        Assert.Equal("standard-shader", platformSettings.SchemaId);
        Assert.Equal("1.0", platformSettings.FieldValues["roughness"]);
        Assert.Equal("0.0", platformSettings.FieldValues["metallic"]);
        Assert.Equal("0.5", platformSettings.FieldValues["specular"]);
        Assert.False(string.IsNullOrWhiteSpace(platformSettings.FieldValues["roughness-texture-id"]));
    }
}
