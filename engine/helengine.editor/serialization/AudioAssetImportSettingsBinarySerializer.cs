namespace helengine.editor {
    /// <summary>
    /// Serializes audio asset import settings using the editor binary header format.
    /// </summary>
    public static class AudioAssetImportSettingsBinarySerializer {
        /// <summary>
        /// Record kind used for audio asset import settings payloads.
        /// </summary>
        public const EditorBinaryRecordKind RecordKind = EditorBinaryRecordKind.AssetImportSettings;

        /// <summary>
        /// Serializer version for the current audio asset import settings payload layout.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Payload endianness used by the current audio asset import settings format.
        /// </summary>
        static readonly EngineBinaryEndianness PayloadEndianness = EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Serializes audio asset import settings to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="settings">Settings instance to serialize.</param>
        public static void Serialize(Stream stream, AudioAssetImportSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Audio asset import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Audio asset import settings must include processor platform settings.");
            }

            EngineBinaryHeader header = new EngineBinaryHeader(
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.AudioAssetImportSettings);
            EngineBinaryHeaderSerializer.Write(stream, header);

            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, PayloadEndianness);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, AudioAssetProcessorSettings> entry in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Audio asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Audio asset import settings must include processor settings for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                WriteProcessorSettings(writer, entry.Value, entry.Key);
            }
        }

        /// <summary>
        /// Deserializes audio asset import settings from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the payload.</param>
        /// <returns>Deserialized settings instance.</returns>
        public static AudioAssetImportSettings Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            ValidateHeader(header);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, header.Endianness);
            if (header.ValueKind != (ushort)AssetImportSettingsBinaryValueKind.AudioAssetImportSettings) {
                throw new InvalidOperationException($"Unexpected audio asset import settings value kind '{header.ValueKind}'.");
            }

            AudioAssetImportSettings settings = new AudioAssetImportSettings();
            settings.Importer.ImporterId = reader.ReadString();
            settings.Importer.SourceChecksum = reader.ReadString();
            settings.Importer.AssetId = reader.ReadString();

            int platformCount = reader.ReadInt32();
            if (platformCount < 0) {
                throw new InvalidOperationException("Audio asset import settings platform count cannot be negative.");
            }

            for (int index = 0; index < platformCount; index++) {
                string platformId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new InvalidOperationException("Audio asset import settings cannot contain a blank processor platform id.");
                }

                settings.Processor.Platforms.Add(platformId, ReadProcessorSettings(reader, platformId));
            }

            return settings;
        }

        /// <summary>
        /// Validates that the provided header matches the audio asset import settings format.
        /// </summary>
        /// <param name="header">Header metadata to validate.</param>
        static void ValidateHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            } else if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                throw new InvalidOperationException($"Unsupported audio asset import settings format id '{header.FormatId}'.");
            } else if (header.RecordKind != (ushort)RecordKind) {
                throw new InvalidOperationException($"Unexpected audio asset import settings record kind '{header.RecordKind}'.");
            } else if (header.Version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported audio asset import settings binary version '{header.Version}'.");
            }
        }

        /// <summary>
        /// Writes one platform audio processor settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Processor settings to serialize.</param>
        /// <param name="platformId">Owning platform identifier for diagnostics.</param>
        static void WriteProcessorSettings(EngineBinaryWriter writer, AudioAssetProcessorSettings settings, string platformId) {
            if (string.IsNullOrWhiteSpace(settings.EncodingFamilyId)) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a blank encoding family id for platform '{platformId}'.");
            } else if (settings.TargetSampleRate < 0) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a negative target sample rate for platform '{platformId}'.");
            } else if (settings.StreamChunkByteSize < 0) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a negative stream chunk size for platform '{platformId}'.");
            } else if (string.IsNullOrWhiteSpace(settings.DefaultBusId)) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a blank default bus id for platform '{platformId}'.");
            }

            writer.WriteString(settings.EncodingFamilyId);
            writer.WriteByte((byte)settings.PlaybackMode);
            writer.WriteUInt16(settings.TargetChannels);
            writer.WriteInt32(settings.TargetSampleRate);
            writer.WriteInt32(settings.StreamChunkByteSize);
            writer.WriteByte(settings.DefaultLoop ? (byte)1 : (byte)0);
            writer.WriteString(settings.DefaultBusId);
        }

        /// <summary>
        /// Reads one platform audio processor settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <param name="platformId">Owning platform identifier for diagnostics.</param>
        /// <returns>Deserialized audio processor settings.</returns>
        static AudioAssetProcessorSettings ReadProcessorSettings(EngineBinaryReader reader, string platformId) {
            AudioAssetProcessorSettings settings = new AudioAssetProcessorSettings {
                EncodingFamilyId = reader.ReadString(),
                PlaybackMode = (AudioPlaybackMode)reader.ReadByte(),
                TargetChannels = reader.ReadUInt16(),
                TargetSampleRate = reader.ReadInt32(),
                StreamChunkByteSize = reader.ReadInt32(),
                DefaultLoop = reader.ReadByte() != 0,
                DefaultBusId = reader.ReadString()
            };
            if (string.IsNullOrWhiteSpace(settings.EncodingFamilyId)) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a blank encoding family id for platform '{platformId}'.");
            } else if (settings.TargetSampleRate < 0) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a negative target sample rate for platform '{platformId}'.");
            } else if (settings.StreamChunkByteSize < 0) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a negative stream chunk size for platform '{platformId}'.");
            } else if (string.IsNullOrWhiteSpace(settings.DefaultBusId)) {
                throw new InvalidOperationException($"Audio asset import settings cannot contain a blank default bus id for platform '{platformId}'.");
            }

            return settings;
        }
    }
}
