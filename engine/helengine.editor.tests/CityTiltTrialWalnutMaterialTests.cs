namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored Tilt Trial walnut material keeps the GameCube texture binding required by the packaged sphere runtime.
/// </summary>
public sealed class CityTiltTrialWalnutMaterialTests {
    /// <summary>
    /// Absolute authored walnut material path generated for the Tilt Trial player sphere.
    /// </summary>
    const string TiltTrialWalnutMaterialPath = @"C:\dev\helprojs\city\assets\materials\rendering\tilt_trial\PlayerSphereWalnut.hasset";

    /// <summary>
    /// Ensures the authored GameCube walnut material keeps a diffuse texture asset id plus the cooked runtime texture path required by the packager.
    /// </summary>
    [Fact]
    public void Tilt_trial_walnut_material_source_preserves_gamecube_runtime_texture_binding() {
        MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();

        Assert.True(File.Exists(TiltTrialWalnutMaterialPath), $"Expected Tilt Trial walnut material at '{TiltTrialWalnutMaterialPath}'.");

        ShaderMaterialAsset materialAsset = settingsService.LoadMaterialAsset(TiltTrialWalnutMaterialPath, "gamecube");
        bool loaded = settingsService.TryLoadPlatformSettings(TiltTrialWalnutMaterialPath, "gamecube", out MaterialAssetProcessorSettings platformSettings);

        Assert.True(loaded);
        Assert.NotNull(platformSettings);
        Assert.Equal("gamecube-standard-textured", platformSettings.SchemaId);
        Assert.False(string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId));
        Assert.True(platformSettings.FieldValues.TryGetValue("texture-relative-path", out string textureRelativePath));
        Assert.Equal("cooked/imported/" + materialAsset.DiffuseTextureAssetId, textureRelativePath);
    }

    /// <summary>
    /// Ensures the authored Windows walnut material keeps the player sphere double-sided so the rolling textured mesh never presents as partially see-through from backface culling.
    /// </summary>
    [Fact]
    public void Tilt_trial_walnut_material_source_uses_double_sided_windows_render_state() {
        MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();

        Assert.True(File.Exists(TiltTrialWalnutMaterialPath), $"Expected Tilt Trial walnut material at '{TiltTrialWalnutMaterialPath}'.");

        ShaderMaterialAsset materialAsset = settingsService.LoadMaterialAsset(TiltTrialWalnutMaterialPath, "windows");

        Assert.NotNull(materialAsset.RenderState);
        Assert.Equal(MaterialCullMode.None, materialAsset.RenderState.CullMode);
    }
}
