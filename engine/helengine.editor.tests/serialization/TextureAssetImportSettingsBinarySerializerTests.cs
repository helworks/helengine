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
    }
}
