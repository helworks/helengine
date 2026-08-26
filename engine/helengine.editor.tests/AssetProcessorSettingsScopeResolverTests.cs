namespace helengine.editor.tests {
    public sealed class AssetProcessorSettingsScopeResolverTests {
        [Fact]
        public void Resolve_WhenEnvironmentOverridesOneSection_UsesEnvironmentSectionAndInheritsOtherSections() {
            AssetProcessorSettings settings = new AssetProcessorSettings();
            AssetPlatformProcessorSettings platform = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = true,
                    Tessellate = false
                },
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 512
                }
            };
            platform.Environments["debug"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings {
                    FlipWinding = false,
                    Tessellate = true
                }
            };
            settings.Platforms["windows"] = platform;

            AssetPlatformProcessorSettings resolved = AssetProcessorSettingsScopeResolver.Resolve(
                settings,
                new EditorOverrideScope("windows", "debug"));

            Assert.False(resolved.Model.FlipWinding);
            Assert.True(resolved.Model.Tessellate);
            Assert.Equal(512, resolved.Texture.MaxResolution);
        }

        [Fact]
        public void BinarySerializer_RoundTripsEnvironmentProcessorSections() {
            AssetImportSettings settings = new AssetImportSettings {
                Processor = new AssetProcessorSettings()
            };
            settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings { Tessellate = false }
            };
            settings.Processor.Platforms["windows"].Environments["debug"] = new AssetPlatformProcessorSettings {
                Model = new ModelAssetProcessorSettings { Tessellate = true }
            };

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            AssetImportSettings restored = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);
            Assert.True(restored.Processor.Platforms["windows"].Environments["debug"].Model.Tessellate);
        }
    }
}
