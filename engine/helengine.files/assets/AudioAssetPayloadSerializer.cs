using helengine;

namespace helengine.files {
    /// <summary>
    /// Serializes the payload body of an audio asset for the editor asset format, including its chunk table and its platform-authored overrides.
    /// </summary>
    public sealed class AudioAssetPayloadSerializer : IEditorAssetPayloadSerializer {
        /// <summary>
        /// Gets the value kind stamped into the header for audio asset payloads.
        /// </summary>
        public EditorAssetBinaryValueKind ValueKind {
            get {
                return EditorAssetBinaryValueKind.AudioAsset;
            }
        }

        /// <summary>
        /// Reports whether the supplied asset is an audio asset.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        /// <returns>True when the asset is an audio asset.</returns>
        public bool Handles(Asset asset) {
            return asset is AudioAsset;
        }

        /// <summary>
        /// Performs no validation because an audio payload carries no cross-record invariants that must hold before the header is written.
        /// </summary>
        /// <param name="asset">Asset instance about to be serialized.</param>
        public void Validate(Asset asset) {
        }

        /// <summary>
        /// Writes an audio asset payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Audio asset to serialize.</param>
        public void Write(EngineBinaryWriter writer, Asset asset) {
            AudioAsset audioAsset = (AudioAsset)asset;
            EditorAssetPayloadPrimitives.EnsureRuntimeAssetIdentity(audioAsset);
            EditorAssetPayloadPrimitives.WriteAssetIdentity(writer, audioAsset);
            writer.WriteByte((byte)audioAsset.PlaybackMode);
            writer.WriteByte(audioAsset.DefaultLoop ? (byte)1 : (byte)0);
            writer.WriteString(audioAsset.DefaultBusId);
            writer.WriteInt32(audioAsset.Channels);
            writer.WriteInt32(audioAsset.SampleRate);
            writer.WriteSingle(audioAsset.DurationSeconds);
            writer.WriteString(audioAsset.EncodingFamilyId);
            writer.WriteByteArray(audioAsset.EncodedBytes);
            writer.WriteArray(audioAsset.Chunks, WriteAudioChunkDescriptor);
            writer.WriteArray(audioAsset.PlatformOverrides?
                .OrderBy(platformOverride => platformOverride?.PlatformId ?? string.Empty, StringComparer.Ordinal)
                .ToArray(), WriteAudioAssetPlatformOverrideAsset);
        }

        /// <summary>
        /// Reads an audio asset payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized audio asset.</returns>
        public Asset Read(EngineBinaryReader reader) {
            AudioAsset asset = new AudioAsset();
            EditorAssetPayloadPrimitives.ReadAssetIdentity(reader, asset);
            asset.PlaybackMode = (AudioPlaybackMode)reader.ReadByte();
            asset.DefaultLoop = reader.ReadByte() != 0;
            asset.DefaultBusId = reader.ReadString();
            asset.Channels = reader.ReadInt32();
            asset.SampleRate = reader.ReadInt32();
            asset.DurationSeconds = reader.ReadSingle();
            asset.EncodingFamilyId = reader.ReadString();
            asset.EncodedBytes = reader.ReadByteArray() ?? Array.Empty<byte>();
            asset.Chunks = reader.ReadArray(ReadAudioChunkDescriptor) ?? Array.Empty<AudioChunkDescriptor>();
            asset.PlatformOverrides = reader.ReadArray(ReadAudioAssetPlatformOverrideAsset) ?? Array.Empty<AudioAssetPlatformOverrideAsset>();
            return asset;
        }

        /// <summary>
        /// Writes one audio chunk descriptor payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Audio chunk descriptor to serialize.</param>
        static void WriteAudioChunkDescriptor(EngineBinaryWriter writer, AudioChunkDescriptor asset) {
            writer.WriteInt32(asset.ByteOffset);
            writer.WriteInt32(asset.ByteLength);
        }

        /// <summary>
        /// Reads one audio chunk descriptor payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized audio chunk descriptor.</returns>
        static AudioChunkDescriptor ReadAudioChunkDescriptor(EngineBinaryReader reader) {
            return new AudioChunkDescriptor {
                ByteOffset = reader.ReadInt32(),
                ByteLength = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Writes one platform-authored audio override payload.
        /// </summary>
        /// <param name="writer">Destination writer for the payload.</param>
        /// <param name="asset">Platform-authored audio override to serialize.</param>
        static void WriteAudioAssetPlatformOverrideAsset(EngineBinaryWriter writer, AudioAssetPlatformOverrideAsset asset) {
            writer.WriteString(asset.PlatformId);
            writer.WriteByte((byte)asset.PlaybackMode);
            writer.WriteByte(asset.DefaultLoop ? (byte)1 : (byte)0);
            writer.WriteString(asset.DefaultBusId);
            writer.WriteInt32(asset.Channels);
            writer.WriteInt32(asset.SampleRate);
            writer.WriteSingle(asset.DurationSeconds);
            writer.WriteString(asset.EncodingFamilyId);
            writer.WriteByteArray(asset.EncodedBytes);
            writer.WriteArray(asset.Chunks, WriteAudioChunkDescriptor);
        }

        /// <summary>
        /// Reads one platform-authored audio override payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the payload.</param>
        /// <returns>Deserialized platform-authored audio override.</returns>
        static AudioAssetPlatformOverrideAsset ReadAudioAssetPlatformOverrideAsset(EngineBinaryReader reader) {
            return new AudioAssetPlatformOverrideAsset {
                PlatformId = reader.ReadString(),
                PlaybackMode = (AudioPlaybackMode)reader.ReadByte(),
                DefaultLoop = reader.ReadByte() != 0,
                DefaultBusId = reader.ReadString(),
                Channels = reader.ReadInt32(),
                SampleRate = reader.ReadInt32(),
                DurationSeconds = reader.ReadSingle(),
                EncodingFamilyId = reader.ReadString(),
                EncodedBytes = reader.ReadByteArray() ?? Array.Empty<byte>(),
                Chunks = reader.ReadArray(ReadAudioChunkDescriptor) ?? Array.Empty<AudioChunkDescriptor>()
            };
        }
    }
}
