using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies HELE serialization behavior for audio assets.
    /// </summary>
    public sealed class AudioAssetSerializationTests {
        /// <summary>
        /// Ensures audio assets round-trip through the HELE serializer with metadata, payload bytes, and chunk descriptors intact.
        /// </summary>
        [Fact]
        public void AssetSerializer_AudioAsset_RoundTripsMetadataAndPayload() {
            AudioAsset asset = new AudioAsset {
                Id = "Audio/MenuTheme.haudio",
                RuntimeAssetId = 1234UL,
                PlaybackMode = AudioPlaybackMode.Streamed,
                DefaultLoop = true,
                DefaultBusId = "music",
                Channels = 2,
                SampleRate = 44100,
                DurationSeconds = 12.5f,
                EncodingFamilyId = "pcm-streamed",
                EncodedBytes = [1, 2, 3, 4],
                Chunks = [
                    new AudioChunkDescriptor {
                        ByteOffset = 0,
                        ByteLength = 4
                    }
                ]
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            AudioAsset deserialized = Assert.IsType<AudioAsset>(AssetSerializer.DeserializeFromBytes(data));

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)EditorAssetBinaryValueKind.AudioAsset, header.ValueKind);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.RuntimeAssetId, deserialized.RuntimeAssetId);
            Assert.Equal(AudioPlaybackMode.Streamed, deserialized.PlaybackMode);
            Assert.True(deserialized.DefaultLoop);
            Assert.Equal("music", deserialized.DefaultBusId);
            Assert.Equal(2, deserialized.Channels);
            Assert.Equal(44100, deserialized.SampleRate);
            Assert.Equal(12.5f, deserialized.DurationSeconds);
            Assert.Equal("pcm-streamed", deserialized.EncodingFamilyId);
            Assert.Equal([1, 2, 3, 4], deserialized.EncodedBytes);

            AudioChunkDescriptor chunk = Assert.Single(deserialized.Chunks);
            Assert.Equal(0, chunk.ByteOffset);
            Assert.Equal(4, chunk.ByteLength);
        }

        static EngineBinaryHeader ReadHeader(byte[] data) {
            using MemoryStream stream = new MemoryStream(data, false);
            return EngineBinaryHeaderSerializer.Read(stream);
        }
    }
}
