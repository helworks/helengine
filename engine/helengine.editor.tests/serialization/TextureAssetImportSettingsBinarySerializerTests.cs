using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies typed texture import-settings binary serialization behavior.
    /// </summary>
    public sealed class TextureAssetImportSettingsBinarySerializerTests {
        /// <summary>
        /// Verifies native GameCube indexed settings retain existing three-bit alpha in platform and environment records.
        /// </summary>
        /// <param name="useEnvironment">Whether to store the texture settings in an environment override.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SerializeDeserialize_WhenGameCubeUsesIndexedA3_RoundTripsAlphaPrecision(bool useEnvironment) {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            TextureAssetProcessorSettings textureSettings = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormatId = "Gx.CI4.TLUT.RGB5A3",
                AlphaPrecision = TextureAssetAlphaPrecision.A3
            };
            if (useEnvironment) {
                settings.Processor.Environments["gamecube"] = new Dictionary<string, TextureAssetProcessorSettings> {
                    ["preview"] = textureSettings
                };
            } else {
                settings.Processor.Platforms["gamecube"] = textureSettings;
            }

            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            TextureAssetImportSettings restored = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

            TextureAssetProcessorSettings restoredTexture = useEnvironment
                ? restored.Processor.Environments["gamecube"]["preview"]
                : restored.Processor.Platforms["gamecube"];
            Assert.Equal("Gx.CI4.TLUT.RGB5A3", restoredTexture.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A3, restoredTexture.AlphaPrecision);
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>
        /// Verifies undefined alpha bytes cannot be written as valid platform or environment settings.
        /// </summary>
        /// <param name="useEnvironment">Whether to store the invalid alpha in an environment override.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_WhenAlphaPrecisionIsUndefined_Throws(bool useEnvironment) {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            TextureAssetProcessorSettings textureSettings = new TextureAssetProcessorSettings {
                AlphaPrecision = (TextureAssetAlphaPrecision)255
            };
            if (useEnvironment) {
                settings.Processor.Environments["gamecube"] = new Dictionary<string, TextureAssetProcessorSettings> {
                    ["preview"] = textureSettings
                };
            } else {
                settings.Processor.Platforms["gamecube"] = textureSettings;
            }

            using MemoryStream stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings));
        }

        /// <summary>
        /// Verifies malformed alpha bytes are rejected while reading an otherwise valid settings document.
        /// </summary>
        [Fact]
        public void Deserialize_WhenAlphaPrecisionIsUndefined_Throws() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Processor.Platforms["gamecube"] = new TextureAssetProcessorSettings();
            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            EngineBinaryHeaderSerializer.Read(stream);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            reader.ReadString();
            reader.ReadInt32();
            reader.ReadString();
            reader.ReadInt32();
            reader.ReadString();
            stream.WriteByte(255);
            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => TextureAssetImportSettingsBinarySerializer.Deserialize(stream));
            Assert.Contains("Unsupported texture alpha precision '255'", exception.Message);
        }

        /// <summary>
        /// Verifies typed texture sidecars preserve DS texture format, alpha precision, and resolution settings.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_WhenDsTextureSettingsUseIndexedFormat_RoundTripsAllProcessorFields() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            settings.Importer.SourceChecksum = "sha256:test";
            settings.Importer.ProcessedAssetId = "asset/test";
            settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A4
            };

            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            TextureAssetImportSettings roundTripped = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

            TextureAssetProcessorSettings dsSettings = Assert.Single(roundTripped.Processor.Platforms).Value;
            Assert.Equal(256, dsSettings.MaxResolution);
            Assert.Equal(TextureAssetColorFormat.Indexed8, dsSettings.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.A4, dsSettings.AlphaPrecision);
        }

        /// <summary>
        /// Verifies typed texture sidecars preserve the selected indexing method for shared indexed texture formats.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_WhenIndexedTextureSettingsUseQuantizedIndexed_RoundTripsIndexingMethod() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            settings.Importer.SourceChecksum = "sha256:test";
            settings.Importer.ProcessedAssetId = "asset/test";
            settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A4,
                IndexingMethodId = TextureAssetIndexingMethod.QuantizedIndexed.ToString()
            };

            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            TextureAssetImportSettings roundTripped = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

            TextureAssetProcessorSettings dsSettings = Assert.Single(roundTripped.Processor.Platforms).Value;
            Assert.Equal(TextureAssetIndexingMethod.QuantizedIndexed.ToString(), dsSettings.IndexingMethodId);
        }

        /// <summary>
        /// Verifies typed texture sidecars preserve opaque platform-owned texture color-format identifiers.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_WhenGameCubeUsesOpaqueColorFormatId_PreservesThatFormat() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            settings.Importer.SourceChecksum = "sha256:test";
            settings.Importer.ProcessedAssetId = "asset/test";
            settings.Processor.Platforms["gamecube"] = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormatId = "GxRgb5A3",
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            };

            using MemoryStream stream = new MemoryStream();
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;

            TextureAssetImportSettings roundTripped = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);

            TextureAssetProcessorSettings gamecubeSettings = Assert.Single(roundTripped.Processor.Platforms).Value;
            Assert.Equal(256, gamecubeSettings.MaxResolution);
            Assert.Equal("GxRgb5A3", gamecubeSettings.ColorFormatId);
            Assert.Equal(TextureAssetAlphaPrecision.A8, gamecubeSettings.AlphaPrecision);
        }
    }
}
