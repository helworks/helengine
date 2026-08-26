using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies section-based asset import settings serialization.
    /// </summary>
    public sealed class SectionedAssetImportSettingsBinarySerializerTests {
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
                },
                AssetReferenceValues = new Dictionary<string, SceneAssetReference>(StringComparer.OrdinalIgnoreCase) {
                    ["BaseTexture"] = global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                        "11111111222243338444555555555555",
                        "textures/base.png",
                        "sha256:" + new string('a', 64))
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
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            AssetImportSettings deserialized = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);
            FontAssetProcessorSettings fontSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<FontAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "font");
            TextureAssetProcessorSettings fontAtlasSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<TextureAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "font-atlas-texture");
            ModelAssetProcessorSettings modelSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<ModelAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "model");
            MaterialAssetProcessorSettings materialSettings = AssetPlatformSettingsSectionRegistry.Shared.GetOrCreateSection<MaterialAssetProcessorSettings>(
                deserialized.Processor.Platforms["windows"],
                "material");

            Assert.Equal(14, fontSettings.PixelSize);
            Assert.Equal(TextureAssetColorFormat.Indexed4, fontAtlasSettings.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, fontAtlasSettings.AlphaPrecision);
            Assert.True(modelSettings.FlipWinding);
            Assert.True(modelSettings.Tessellate);
            Assert.Equal(0.5d, modelSettings.TessellationMaxEdgeLength);
            SceneAssetReference baseTexture = Assert.IsType<SceneAssetReference>(materialSettings.AssetReferenceValues["BaseTexture"]);
            Assert.Equal("11111111222243338444555555555555", baseTexture.AssetId);
            Assert.Equal("textures/base.png", baseTexture.RelativePath);
            Assert.Equal("sha256:" + new string('a', 64), baseTexture.ContentHash);
        }

        /// <summary>
        /// Ensures any non-current generic settings payload is rejected.
        /// </summary>
        [Fact]
        public void Deserialize_WhenPayloadIsNotCurrent_Throws() {
            using MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                9,
                EditorAssetBinarySerializer.FormatId,
                (ushort)SectionedAssetImportSettingsBinarySerializer.RecordKind,
                (ushort)SectionedAssetImportSettingsBinarySerializer.ValueKind);
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

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => SectionedAssetImportSettingsBinarySerializer.Deserialize(stream));

            Assert.Contains("9", exception.Message, StringComparison.Ordinal);
            Assert.Contains(SectionedAssetImportSettingsBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
