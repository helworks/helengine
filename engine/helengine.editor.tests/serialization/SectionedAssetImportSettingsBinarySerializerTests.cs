using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies section-based asset import settings serialization.
    /// </summary>
    public sealed class SectionedAssetImportSettingsBinarySerializerTests {
        /// <summary>
        /// Verifies registered texture sections retain native GameCube three-bit alpha in platform and environment settings.
        /// </summary>
        /// <param name="useEnvironment">Whether to store the texture section in an environment override.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SerializeDeserialize_WhenGameCubeTextureUsesA3_RoundTripsAlphaPrecision(bool useEnvironment) {
            AssetImportSettings settings = new AssetImportSettings();
            AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();
            settings.Processor.Platforms["gamecube"] = platformSettings;
            AssetPlatformProcessorSettings textureOwner = platformSettings;
            if (useEnvironment) {
                textureOwner = new AssetPlatformProcessorSettings();
                platformSettings.Environments["preview"] = textureOwner;
            }
            textureOwner.Texture = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormatId = "Gx.CI4.TLUT.RGB5A3",
                AlphaPrecision = TextureAssetAlphaPrecision.A3
            };

            using MemoryStream stream = new MemoryStream();
            SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            AssetImportSettings restored = SectionedAssetImportSettingsBinarySerializer.Deserialize(stream);

            AssetPlatformProcessorSettings restoredOwner = useEnvironment
                ? restored.Processor.Platforms["gamecube"].Environments["preview"]
                : restored.Processor.Platforms["gamecube"];
            Assert.Equal("Gx.CI4.TLUT.RGB5A3", restoredOwner.Texture.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A3, restoredOwner.Texture.AlphaPrecision);
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>
        /// Verifies the registered texture section rejects undefined alpha bytes during deserialization.
        /// </summary>
        [Fact]
        public void DeserializeTextureSection_WhenAlphaPrecisionIsUndefined_Throws() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteInt32(256);
            writer.WriteString("Gx.CI4.TLUT.RGB5A3");
            writer.WriteByte(255);
            writer.WriteString(string.Empty);
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new TextureAssetPlatformSettingsSectionDefinition().Deserialize(reader));
            Assert.Contains("Unsupported texture alpha precision '255'", exception.Message);
        }

        /// <summary>
        /// Ensures all built-in platform sections survive one binary roundtrip.
        /// </summary>
        [Fact]
        public void Serialize_WhenProcessorUsesSectionRegistry_RoundtripsBuiltInSections() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "test-font";
            settings.Importer.SourceChecksum = "abc123";
            settings.Importer.ProcessedAssetId = "asset-id";

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
