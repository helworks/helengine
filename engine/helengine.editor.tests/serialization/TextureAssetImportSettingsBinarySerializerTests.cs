using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies typed texture import-settings binary serialization behavior.
    /// </summary>
    public sealed class TextureAssetImportSettingsBinarySerializerTests {
        /// <summary>
        /// Verifies typed texture sidecars preserve DS texture format, alpha precision, and resolution settings.
        /// </summary>
        [Fact]
        public void SerializeDeserialize_WhenDsTextureSettingsUseIndexedFormat_RoundTripsAllProcessorFields() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            settings.Importer.SourceChecksum = "sha256:test";
            settings.Importer.AssetId = "asset/test";
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
            settings.Importer.AssetId = "asset/test";
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
            settings.Importer.AssetId = "asset/test";
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
