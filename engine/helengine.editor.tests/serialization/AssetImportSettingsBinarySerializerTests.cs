using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies section-based asset import settings serialization.
    /// </summary>
    public sealed class AssetImportSettingsBinarySerializerTests {
        /// <summary>
        /// Ensures all built-in platform sections survive one binary roundtrip.
        /// </summary>
        [Fact]
        public void Serialize_WhenProcessorUsesSectionRegistry_RoundtripsBuiltInSections() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "test-font";
            settings.Importer.SourceChecksum = "abc123";
            settings.Importer.AssetId = "asset-id";

            AssetPlatformProcessorSettings windowsSettings = new AssetPlatformProcessorSettings();
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(windowsSettings, "texture", new TextureAssetProcessorSettings {
                MaxResolution = 128,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            });
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(windowsSettings, "model", new ModelAssetProcessorSettings {
                FlipWinding = true,
                Tessellate = true,
                TessellationMaxEdgeLength = 0.5d
            });
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(windowsSettings, "material", new MaterialAssetProcessorSettings {
                SchemaId = "lit",
                FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    ["BaseColor"] = "#ffffff"
                }
            });
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(windowsSettings, "font", new FontAssetProcessorSettings {
                PixelSize = 14
            });
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(windowsSettings, "font-atlas-texture", new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormat = TextureAssetColorFormat.Indexed4,
                AlphaPrecision = TextureAssetAlphaPrecision.Binary,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            });
            settings.Processor.Platforms["windows"] = windowsSettings;

            using MemoryStream stream = new MemoryStream();
            AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);
            FontAssetProcessorSettings fontSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<FontAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "font");
            TextureAssetProcessorSettings fontAtlasSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TextureAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "font-atlas-texture");
            ModelAssetProcessorSettings modelSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<ModelAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "model");

            Assert.Equal(14, fontSettings.PixelSize);
            Assert.Equal(TextureAssetColorFormat.Indexed4, fontAtlasSettings.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, fontAtlasSettings.AlphaPrecision);
            Assert.True(modelSettings.FlipWinding);
            Assert.True(modelSettings.Tessellate);
            Assert.Equal(0.5d, modelSettings.TessellationMaxEdgeLength);
        }

        /// <summary>
        /// Ensures version-nine generic settings payloads default newly added model tessellation settings.
        /// </summary>
        [Fact]
        public void Deserialize_WhenPayloadIsVersionNine_DefaultsModelTessellationSettings() {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                9,
                EditorAssetBinarySerializer.FormatId,
                (ushort)AssetImportSettingsBinarySerializer.RecordKind,
                (ushort)AssetImportSettingsBinarySerializer.ValueKind);
            EngineBinaryHeaderSerializer.Write(stream, header);
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true)) {
                writer.WriteString("assimp");
                writer.WriteString("legacy-checksum");
                writer.WriteString("legacy-model-id");
                writer.WriteInt32(1);
                writer.WriteString("windows");
                writer.WriteInt32(1);
                writer.WriteString(ModelAssetPlatformSettingsSectionDefinition.SectionIdValue);
                writer.WriteByte(1);
            }

            stream.Position = 0;

            AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);
            ModelAssetProcessorSettings modelSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<ModelAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                ModelAssetPlatformSettingsSectionDefinition.SectionIdValue);

            Assert.True(modelSettings.FlipWinding);
            Assert.False(modelSettings.Tessellate);
            Assert.Equal(1.0d, modelSettings.TessellationMaxEdgeLength);
        }
    }
}
