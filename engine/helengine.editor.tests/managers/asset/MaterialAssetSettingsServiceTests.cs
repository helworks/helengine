namespace helengine.editor {
    /// <summary>
    /// Verifies generated material settings persistence behavior used by city-authored material generators.
    /// </summary>
    public sealed class MaterialAssetSettingsServiceTests {
        /// <summary>
        /// Ensures one non-empty shared texture id survives save/load when every platform publishes the same authored value.
        /// </summary>
        [Fact]
        public void Save_WhenTextureIdIsSharedAcrossPlatforms_PreservesTextureIdWhenLoadingWindowsMaterial() {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-material-settings-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectoryPath);

            try {
                string materialAssetPath = Path.Combine(tempDirectoryPath, "Cube00.hasset");
                MaterialAssetSettingsService service = new MaterialAssetSettingsService();
                MaterialAssetImportSettings settings = CreateSharedTextureSettings("imported-texture-id");

                service.Save(materialAssetPath, settings);

                ShaderMaterialAsset materialAsset = service.LoadMaterialAsset(materialAssetPath, "windows");
                Assert.Equal("imported-texture-id", materialAsset.DiffuseTextureAssetId);

                Assert.True(service.TryLoadPlatformSettings(materialAssetPath, "windows", out MaterialAssetProcessorSettings platformSettings));
                Assert.Equal("imported-texture-id", platformSettings.FieldValues["texture-id"]);
            } finally {
                if (Directory.Exists(tempDirectoryPath)) {
                    Directory.Delete(tempDirectoryPath, true);
                }
            }
        }

        /// <summary>
        /// Creates one representative generated material settings payload that matches the city textured cube-grid generator.
        /// </summary>
        /// <param name="textureAssetId">Shared authored texture asset id that should survive save/load.</param>
        /// <returns>Representative generated material settings payload.</returns>
        static MaterialAssetImportSettings CreateSharedTextureSettings(string textureAssetId) {
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = "helengine.material";
            settings.Importer.SourceChecksum = string.Empty;
            settings.Importer.AssetId = "Materials.rendering.textured_cube_grid.Cube00";

            MaterialAssetProcessorSettings windowsSettings = new MaterialAssetProcessorSettings();
            windowsSettings.SchemaId = "standard-shader";
            windowsSettings.FieldValues["use-custom-shader"] = "false";
            windowsSettings.FieldValues["shader-asset-id"] = "ForwardStandardShader";
            windowsSettings.FieldValues["texture-id"] = textureAssetId;
            windowsSettings.FieldValues["casts-shadow"] = "true";
            windowsSettings.FieldValues["receives-shadow"] = "true";
            windowsSettings.FieldValues["base-color"] = "#FFFFFFFF";
            settings.Processor.Platforms["windows"] = windowsSettings;

            MaterialAssetProcessorSettings ps2Settings = new MaterialAssetProcessorSettings();
            ps2Settings.SchemaId = "ps2-simple-lit-textured";
            ps2Settings.FieldValues["texture-id"] = textureAssetId;
            ps2Settings.FieldValues["alpha-mode"] = "opaque";
            ps2Settings.FieldValues["double-sided"] = "false";
            ps2Settings.FieldValues["cast-shadows"] = "true";
            ps2Settings.FieldValues["vertex-color-mode"] = "ignore";
            ps2Settings.FieldValues["base-color"] = "#FFFFFFFF";
            settings.Processor.Platforms["ps2"] = ps2Settings;

            MaterialAssetProcessorSettings pspSettings = new MaterialAssetProcessorSettings();
            pspSettings.SchemaId = "standard-shader";
            pspSettings.FieldValues["use-custom-shader"] = "false";
            pspSettings.FieldValues["shader-asset-id"] = "ForwardStandardShader";
            pspSettings.FieldValues["texture-id"] = textureAssetId;
            pspSettings.FieldValues["casts-shadow"] = "true";
            pspSettings.FieldValues["receives-shadow"] = "true";
            pspSettings.FieldValues["base-color"] = "#FFFFFFFF";
            settings.Processor.Platforms["psp"] = pspSettings;

            MaterialAssetProcessorSettings dsSettings = new MaterialAssetProcessorSettings();
            dsSettings.SchemaId = "ds-standard-textured";
            dsSettings.FieldValues["texture-id"] = textureAssetId;
            dsSettings.FieldValues["texture-relative-path"] = "cooked/imported/" + textureAssetId;
            dsSettings.FieldValues["double-sided"] = "false";
            dsSettings.FieldValues["vertex-color-mode"] = "ignore";
            dsSettings.FieldValues["base-color"] = "#FFFFFFFF";
            dsSettings.FieldValues["lighting-mode"] = "lit";
            settings.Processor.Platforms["ds"] = dsSettings;
            return settings;
        }
    }
}
