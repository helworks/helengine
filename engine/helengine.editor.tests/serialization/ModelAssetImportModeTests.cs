using Xunit;

namespace helengine.editor.tests.serialization {
    /// <summary>
    /// Verifies the per-file model import mode persisted by the model asset import settings serializer, including reading sidecars written before the mode existed.
    /// </summary>
    public sealed class ModelAssetImportModeTests {
        /// <summary>
        /// Ensures a new settings instance imports as a single model.
        /// </summary>
        [Fact]
        public void NewSettings_DefaultToSingleModel() {
            Assert.Equal(ModelImportMode.SingleModel, new ModelAssetImportSettings().Mode);
        }

        /// <summary>
        /// Ensures the scene mode survives a serialize and deserialize round trip together with the platform settings.
        /// </summary>
        [Fact]
        public void RoundTrip_preservesSceneMode() {
            ModelAssetImportSettings settings = new ModelAssetImportSettings();
            settings.Importer.ImporterId = "assimp";
            settings.Mode = ModelImportMode.Scene;
            settings.Processor.Platforms.Add("windows", new ModelAssetProcessorSettings { FlipWinding = true });

            using MemoryStream stream = new MemoryStream();
            ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            stream.Position = 0;
            ModelAssetImportSettings restored = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(ModelImportMode.Scene, restored.Mode);
            Assert.Equal("assimp", restored.Importer.ImporterId);
            Assert.True(restored.Processor.Platforms["windows"].FlipWinding);
        }

        /// <summary>
        /// Ensures a version 5 sidecar, which has no mode byte, loads as a single model and keeps every per-platform entry.
        /// </summary>
        [Fact]
        public void V5Sidecar_readsWithSingleModelMode() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(
                stream,
                EngineBinaryEndianness.LittleEndian,
                5,
                EditorAssetBinarySerializer.FormatId,
                (ushort)ModelAssetImportSettingsBinarySerializer.RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings)) {
                writer.WriteString("assimp");
                writer.WriteInt32(2);
                WritePlatform(writer, "ps2", false, false, 1.0d);
                WritePlatform(writer, "windows", true, true, 0.25d);
                writer.WriteInt32(0);
            }

            stream.Position = 0;
            ModelAssetImportSettings restored = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);

            Assert.Equal(ModelImportMode.SingleModel, restored.Mode);
            Assert.Equal("assimp", restored.Importer.ImporterId);
            Assert.Equal(2, restored.Processor.Platforms.Count);
            Assert.True(restored.Processor.Platforms["windows"].FlipWinding);
            Assert.True(restored.Processor.Platforms["windows"].Tessellate);
            Assert.Equal(0.25d, restored.Processor.Platforms["windows"].TessellationMaxEdgeLength);
            Assert.False(restored.Processor.Platforms["ps2"].FlipWinding);
        }

        /// <summary>
        /// Ensures a persisted mode byte that is not a defined mode is rejected.
        /// </summary>
        [Fact]
        public void UnknownMode_throws() {
            using MemoryStream stream = new MemoryStream();
            using (EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(
                stream,
                EngineBinaryEndianness.LittleEndian,
                ModelAssetImportSettingsBinarySerializer.CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)ModelAssetImportSettingsBinarySerializer.RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.ModelAssetImportSettings)) {
                writer.WriteString("assimp");
                writer.WriteByte(9);
                writer.WriteInt32(0);
                writer.WriteInt32(0);
            }

            stream.Position = 0;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ModelAssetImportSettingsBinarySerializer.Deserialize(stream));
            Assert.Contains("9", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures writing an undefined mode fails instead of persisting it.
        /// </summary>
        [Fact]
        public void Serialize_WithUndefinedMode_Throws() {
            ModelAssetImportSettings settings = new ModelAssetImportSettings { Mode = (ModelImportMode)7 };

            using MemoryStream stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings));
        }

        /// <summary>
        /// Ensures the sidecar loader treats a version 5 file as loadable rather than falling back to defaults.
        /// </summary>
        [Fact]
        public void V5Sidecar_isReadableVersion() {
            Assert.True(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(5));
            Assert.True(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(6));
            Assert.False(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(4));
            Assert.True(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(7));
            Assert.False(ModelAssetImportSettingsBinarySerializer.IsReadableVersion(8));
        }

        /// <summary>
        /// Writes one platform entry using the layout shared by versions 5 and 6.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="platformId">Platform id key.</param>
        /// <param name="flipWinding">Flip winding flag.</param>
        /// <param name="tessellate">Tessellation flag.</param>
        /// <param name="edgeLength">Tessellation maximum edge length.</param>
        static void WritePlatform(EngineBinaryWriter writer, string platformId, bool flipWinding, bool tessellate, double edgeLength) {
            writer.WriteString(platformId);
            writer.WriteByte(flipWinding ? (byte)1 : (byte)0);
            writer.WriteByte(tessellate ? (byte)1 : (byte)0);
            writer.WriteDouble(edgeLength);
            writer.WriteByte(0);
        }
    }
}
