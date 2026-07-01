namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated material authoring helpers persist base assets plus per-platform material settings without exposing sidecar document internals to callers.
    /// </summary>
    public sealed class GeneratedMaterialAssetWriteServiceTests {
        /// <summary>
        /// Ensures generated material writes preserve per-platform texture bindings through the shared material-settings load path.
        /// </summary>
        [Fact]
        public void WriteMaterial_WhenWindowsTextureIdIsAssigned_PersistsTextureIdInSavedSettings() {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-generated-material-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectoryPath);

            try {
                GeneratedMaterialAssetDefinition definition = CreateDefinition();
                GeneratedMaterialAssetWriteService writeService = new GeneratedMaterialAssetWriteService();
                writeService.WriteMaterial(tempDirectoryPath, "Materials/TestMaterial.hasset", definition);

                string materialAssetPath = Path.Combine(tempDirectoryPath, "assets", "Materials", "TestMaterial.hasset");
                MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
                ShaderMaterialAsset loadedMaterialAsset = settingsService.LoadMaterialAsset(materialAssetPath, "windows");

                Assert.Equal("Textures/GeneratedChecker", loadedMaterialAsset.DiffuseTextureAssetId);
                Assert.Equal("Materials/TestMaterial", loadedMaterialAsset.Id);
            } finally {
                if (Directory.Exists(tempDirectoryPath)) {
                    Directory.Delete(tempDirectoryPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures generated material writes preserve PS2 imported-texture paths when multiple platform definitions share one authored material.
        /// </summary>
        [Fact]
        public void WriteMaterial_WhenPs2TextureRelativePathIsAssigned_PersistsTexturePathInSavedPs2Settings() {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-generated-material-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectoryPath);

            try {
                GeneratedMaterialAssetDefinition definition = CreateDefinition();
                GeneratedMaterialPlatformDefinition ps2Definition = definition.GetOrCreatePlatform("ps2");
                ps2Definition.SchemaId = "ps2-simple-lit-textured";
                ps2Definition.SetFieldValue("texture-id", "Textures/GeneratedChecker");
                ps2Definition.SetFieldValue("texture-relative-path", "cooked/imported/Textures/GeneratedChecker");
                ps2Definition.SetFieldValue("alpha-mode", "opaque");
                ps2Definition.SetFieldValue("double-sided", "false");
                ps2Definition.SetFieldValue("cast-shadows", "true");
                ps2Definition.SetFieldValue("vertex-color-mode", "ignore");
                ps2Definition.SetFieldValue("base-color", "#FFFFFFFF");

                GeneratedMaterialAssetWriteService writeService = new GeneratedMaterialAssetWriteService();
                writeService.WriteMaterial(tempDirectoryPath, "Materials/TestMaterial.hasset", definition);

                string materialAssetPath = Path.Combine(tempDirectoryPath, "assets", "Materials", "TestMaterial.hasset");
                MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
                MaterialAssetProcessorSettings platformSettings;
                bool loaded = settingsService.TryLoadPlatformSettings(materialAssetPath, "ps2", out platformSettings);

                Assert.True(loaded);
                Assert.NotNull(platformSettings);
                Assert.Equal("ps2-simple-lit-textured", platformSettings.SchemaId);
                Assert.True(platformSettings.FieldValues.TryGetValue("texture-relative-path", out string textureRelativePath));
                Assert.Equal("cooked/imported/Textures/GeneratedChecker", textureRelativePath);
            } finally {
                if (Directory.Exists(tempDirectoryPath)) {
                    Directory.Delete(tempDirectoryPath, true);
                }
            }
        }

        /// <summary>
        /// Creates one representative generated material definition that mirrors the city material-generator contract.
        /// </summary>
        /// <returns>Representative generated material definition.</returns>
        static GeneratedMaterialAssetDefinition CreateDefinition() {
            GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition();
            definition.MaterialAsset = new ShaderMaterialAsset {
                Id = "Materials/TestMaterial",
                RenderState = new MaterialRenderState(),
                CastsShadows = true,
                ReceivesShadows = true
            };

            GeneratedMaterialPlatformDefinition windowsDefinition = definition.GetOrCreatePlatform("windows");
            windowsDefinition.SchemaId = "standard-shader";
            windowsDefinition.SetFieldValue("use-custom-shader", "false");
            windowsDefinition.SetFieldValue("shader-asset-id", "ForwardStandardShader");
            windowsDefinition.SetFieldValue("texture-id", "Textures/GeneratedChecker");
            windowsDefinition.SetFieldValue("casts-shadow", "true");
            windowsDefinition.SetFieldValue("receives-shadow", "true");
            windowsDefinition.SetFieldValue("base-color", "#FFFFFFFF");
            return definition;
        }
    }
}
