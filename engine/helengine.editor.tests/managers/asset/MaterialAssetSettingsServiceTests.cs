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
        /// Ensures raw legacy shader-material payloads are rejected so project materials must use the shared per-platform settings flow.
        /// </summary>
        [Fact]
        public void LoadMaterialAsset_WhenLegacyShaderMaterialPayloadIsStoredInBaseFile_ThrowsForMissingMaterialSettingsDocument() {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-material-settings-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectoryPath);

            try {
                string materialAssetPath = Path.Combine(tempDirectoryPath, "PhysicsDemoBlue.hasset");
                ShaderMaterialAsset sourceMaterialAsset = new ShaderMaterialAsset {
                    Id = "PhysicsDemoBlue",
                    ShaderAssetId = "Shaders.physics.PhysicsDemoMesh",
                    VertexProgram = "PhysicsDemoMesh.vs",
                    PixelProgram = "PhysicsDemoMesh.ps",
                    Variant = "default",
                    ConstantBuffers = new[] {
                        new MaterialConstantBufferAsset {
                            Name = "MaterialColorBuffer",
                            Data = new byte[] {
                                0xC3, 0xF5, 0xA8, 0x3E,
                                0x29, 0x5C, 0x0F, 0x3F,
                                0x66, 0x66, 0x66, 0x3F,
                                0x00, 0x00, 0x80, 0x3F
                            }
                        }
                    },
                    CastsShadows = true,
                    ReceivesShadows = true
                };
                using (FileStream stream = File.Create(materialAssetPath)) {
                    ShaderMaterialAssetBinarySerializer.Serialize(stream, sourceMaterialAsset);
                }

                MaterialAssetSettingsService service = new MaterialAssetSettingsService();
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.LoadMaterialAsset(materialAssetPath, "windows"));
                Assert.Contains("could not be loaded", exception.Message, StringComparison.Ordinal);
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

            MaterialAssetProcessorSettings externalPlatformSettings = new MaterialAssetProcessorSettings();
            externalPlatformSettings.SchemaId = "standard-shader";
            externalPlatformSettings.FieldValues["use-custom-shader"] = "false";
            externalPlatformSettings.FieldValues["shader-asset-id"] = "ForwardStandardShader";
            externalPlatformSettings.FieldValues["texture-id"] = textureAssetId;
            externalPlatformSettings.FieldValues["casts-shadow"] = "true";
            externalPlatformSettings.FieldValues["receives-shadow"] = "true";
            externalPlatformSettings.FieldValues["base-color"] = "#FFFFFFFF";
            settings.Processor.Platforms["external-platform"] = externalPlatformSettings;

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
