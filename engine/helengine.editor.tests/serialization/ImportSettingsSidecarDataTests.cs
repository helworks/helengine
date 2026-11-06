using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies that every source import-settings sidecar serializer (model, texture, audio and the sectioned font/text one) carries the
    /// generic keyed data block: entries owned by nobody in this editor survive a load and save byte-identical, and sidecars written
    /// by the previous version still load with an empty block.
    /// </summary>
    public sealed class ImportSettingsSidecarDataTests {
        /// <summary>
        /// Key of an entry whose owner (for example a plugin) is not loaded in this editor.
        /// </summary>
        const string UnknownKey = "plugin.not.loaded";

        /// <summary>
        /// Raw payload of the unowned entry; it is not a valid payload for any registered owner.
        /// </summary>
        static readonly byte[] UnknownPayload = new byte[] { 200, 0, 1, 2, 255 };

        /// <summary>
        /// Ensures model settings keep an unowned entry byte-identical across a load and a save.
        /// </summary>
        [Fact]
        public void Model_UnknownEntrySurvivesRoundTripByteIdentical() {
            ModelAssetImportSettings settings = new ModelAssetImportSettings();
            settings.Importer.ImporterId = "assimp";
            settings.Processor.Platforms.Add("windows", new ModelAssetProcessorSettings());
            AssertUnknownEntryRoundTrip(
                settings.Importer,
                stream => ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings),
                stream => ModelAssetImportSettingsBinarySerializer.Deserialize(stream).Importer,
                (stream, importer) => {
                    ModelAssetImportSettings restored = new ModelAssetImportSettings { Importer = importer };
                    restored.Processor.Platforms.Add("windows", new ModelAssetProcessorSettings());
                    ModelAssetImportSettingsBinarySerializer.Serialize(stream, restored);
                });
        }

        /// <summary>
        /// Ensures texture settings keep an unowned entry byte-identical across a load and a save.
        /// </summary>
        [Fact]
        public void Texture_UnknownEntrySurvivesRoundTripByteIdentical() {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            AssertUnknownEntryRoundTrip(
                settings.Importer,
                stream => TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings),
                stream => TextureAssetImportSettingsBinarySerializer.Deserialize(stream).Importer,
                (stream, importer) => TextureAssetImportSettingsBinarySerializer.Serialize(stream, new TextureAssetImportSettings { Importer = importer }));
        }

        /// <summary>
        /// Ensures audio settings keep an unowned entry byte-identical across a load and a save.
        /// </summary>
        [Fact]
        public void Audio_UnknownEntrySurvivesRoundTripByteIdentical() {
            AudioAssetImportSettings settings = new AudioAssetImportSettings();
            settings.Importer.ImporterId = "audio";
            AssertUnknownEntryRoundTrip(
                settings.Importer,
                stream => AudioAssetImportSettingsBinarySerializer.Serialize(stream, settings),
                stream => AudioAssetImportSettingsBinarySerializer.Deserialize(stream).Importer,
                (stream, importer) => AudioAssetImportSettingsBinarySerializer.Serialize(stream, new AudioAssetImportSettings { Importer = importer }));
        }

        /// <summary>
        /// Ensures sectioned (font and text) settings keep an unowned entry byte-identical across a load and a save.
        /// </summary>
        [Fact]
        public void Sectioned_UnknownEntrySurvivesRoundTripByteIdentical() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "font";
            AssertUnknownEntryRoundTrip(
                settings.Importer,
                stream => SectionedAssetImportSettingsBinarySerializer.Serialize(stream, settings),
                stream => SectionedAssetImportSettingsBinarySerializer.Deserialize(stream).Importer,
                (stream, importer) => SectionedAssetImportSettingsBinarySerializer.Serialize(stream, new AssetImportSettings { Importer = importer }));
        }

        /// <summary>
        /// Ensures each serializer bumped its version once and still reads the previous version, with an empty data block.
        /// </summary>
        [Fact]
        public void PreviousVersions_LoadWithEmptyBlock() {
            Assert.Equal(7, ModelAssetImportSettingsBinarySerializer.CurrentVersion);
            Assert.Equal(8, TextureAssetImportSettingsBinarySerializer.CurrentVersion);
            Assert.Equal(4, AudioAssetImportSettingsBinarySerializer.CurrentVersion);
            Assert.Equal(15, SectionedAssetImportSettingsBinarySerializer.CurrentVersion);

            ModelAssetImportSettings model = new ModelAssetImportSettings { Mode = ModelImportMode.Scene };
            model.Importer.ImporterId = "assimp";
            byte[] modelBytes = ToPreviousVersion(stream => ModelAssetImportSettingsBinarySerializer.Serialize(stream, model), 6);
            ModelAssetImportSettings restoredModel = ModelAssetImportSettingsBinarySerializer.Deserialize(new MemoryStream(modelBytes));
            Assert.Equal(ModelImportMode.Scene, restoredModel.Mode);
            Assert.Equal(0, restoredModel.Importer.SidecarData.Count);
            Assert.True(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(6));

            TextureAssetImportSettings texture = new TextureAssetImportSettings();
            texture.Importer.ImporterId = "pfim";
            byte[] textureBytes = ToPreviousVersion(stream => TextureAssetImportSettingsBinarySerializer.Serialize(stream, texture), 7);
            Assert.Equal(0, TextureAssetImportSettingsBinarySerializer.Deserialize(new MemoryStream(textureBytes)).Importer.SidecarData.Count);

            AudioAssetImportSettings audio = new AudioAssetImportSettings();
            audio.Importer.ImporterId = "audio";
            byte[] audioBytes = ToPreviousVersion(stream => AudioAssetImportSettingsBinarySerializer.Serialize(stream, audio), 3);
            Assert.Equal(0, AudioAssetImportSettingsBinarySerializer.Deserialize(new MemoryStream(audioBytes)).Importer.SidecarData.Count);

            AssetImportSettings sectioned = new AssetImportSettings();
            sectioned.Importer.ImporterId = "font";
            byte[] sectionedBytes = ToPreviousVersion(stream => SectionedAssetImportSettingsBinarySerializer.Serialize(stream, sectioned), 14);
            Assert.Equal("font", SectionedAssetImportSettingsBinarySerializer.Deserialize(new MemoryStream(sectionedBytes)).Importer.ImporterId);
        }

        /// <summary>
        /// Serializes settings carrying an unowned entry, deserializes them, saves the restored importer settings again and checks that
        /// the entry and the whole file are byte-identical.
        /// </summary>
        /// <param name="importer">Importer settings of the settings being serialized; the unowned entry is added to them.</param>
        /// <param name="serialize">Writes the original settings.</param>
        /// <param name="deserializeImporter">Reads settings back and returns their importer settings.</param>
        /// <param name="serializeRestored">Writes settings built around the restored importer settings.</param>
        static void AssertUnknownEntryRoundTrip(
            AssetImporterSettings importer,
            Action<Stream> serialize,
            Func<Stream, AssetImporterSettings> deserializeImporter,
            Action<Stream, AssetImporterSettings> serializeRestored) {
            importer.SidecarData.SetRaw(UnknownKey, UnknownPayload);
            using MemoryStream original = new MemoryStream();
            serialize(original);
            original.Position = 0;

            AssetImporterSettings restored = deserializeImporter(original);
            using MemoryStream saved = new MemoryStream();
            serializeRestored(saved, restored);

            Assert.True(restored.SidecarData.TryGetRaw(UnknownKey, out byte[] payload));
            Assert.Equal(UnknownPayload, payload);
            Assert.Equal(original.ToArray(), saved.ToArray());
        }

        /// <summary>
        /// Turns a current-version payload with an empty data block into the previous layout: drops the trailing zero entry count and
        /// writes the previous version into the header.
        /// </summary>
        /// <param name="serialize">Writes a current-version payload whose data block is empty.</param>
        /// <param name="previousVersion">Version to write into the header.</param>
        /// <returns>Payload bytes in the previous layout.</returns>
        static byte[] ToPreviousVersion(Action<Stream> serialize, byte previousVersion) {
            using MemoryStream stream = new MemoryStream();
            serialize(stream);
            byte[] bytes = stream.ToArray();
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, bytes.Skip(bytes.Length - 4).ToArray());
            byte[] previous = bytes.Take(bytes.Length - 4).ToArray();
            previous[5] = previousVersion;
            return previous;
        }
    }
}
