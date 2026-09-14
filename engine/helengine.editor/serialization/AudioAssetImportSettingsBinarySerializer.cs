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
        public const byte CurrentVersion = 2;

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

            using EngineBinaryWriter writer = VersionedBinaryPayload.WriteHeader(
                stream,
                PayloadEndianness,
                CurrentVersion,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.AudioAssetImportSettings);
            writer.WriteString(settings.Importer.ImporterId);
            writer.WriteString(settings.Importer.SourceChecksum);
            writer.WriteString(settings.Importer.AssetId);
            writer.WriteInt32(settings.Processor.Platforms.Count);
            foreach (KeyValuePair<string, AudioAssetProcessorSettings> entry in settings.Processor.Platforms.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    throw new InvalidOperationException("Audio asset import settings cannot contain a blank processor platform id.");
                } else if (entry.Value == null) {
                    throw new InvalidOperationException($"Audio asset import settings must include processor settings for platform '{entry.Key}'.");
                }

                writer.WriteString(entry.Key);
                WriteProcessorSettings(writer, entry.Value, entry.Key);
            }
            writer.WriteInt32(settings.Processor.Environments?.Count ?? 0);
            if (settings.Processor.Environments != null) {
                foreach (KeyValuePair<string, Dictionary<string, AudioAssetProcessorSettings>> platformEnvironment in settings.Processor.Environments.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                    writer.WriteString(platformEnvironment.Key);
                    writer.WriteInt32(platformEnvironment.Value?.Count ?? 0);
                    if (platformEnvironment.Value == null) continue;
                    foreach (KeyValuePair<string, AudioAssetProcessorSettings> environmentEntry in platformEnvironment.Value.OrderBy(entry => entry.Key, StringComparer.Ordinal)) {
                        writer.WriteString(environmentEntry.Key);
                        WriteProcessorSettings(writer, environmentEntry.Value, environmentEntry.Key);
                    }
                }
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

            using EngineBinaryReader reader = VersionedBinaryPayload.ReadHeader(
                stream,
                EditorAssetBinarySerializer.FormatId,
                (ushort)RecordKind,
                (ushort)AssetImportSettingsBinaryValueKind.AudioAssetImportSettings,
                CurrentVersion,
                "audio asset import settings",
                "audio asset import settings",
                "audio asset import settings",
                VersionedBinaryVersionMismatchStyle.CurrentVersionSuffix,
                "Regenerate the audio import settings sidecar.");

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

            int environmentPlatformCount = reader.ReadInt32();
            if (environmentPlatformCount < 0) throw new InvalidOperationException("Audio asset import settings environment platform count cannot be negative.");
            for (int index = 0; index < environmentPlatformCount; index++) {
                string platformId = reader.ReadString();
                int environmentCount = reader.ReadInt32();
                if (environmentCount < 0) throw new InvalidOperationException("Audio asset import settings environment count cannot be negative.");
                Dictionary<string, AudioAssetProcessorSettings> environments = new Dictionary<string, AudioAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
                for (int environmentIndex = 0; environmentIndex < environmentCount; environmentIndex++) {
                    string environmentId = reader.ReadString();
                    environments.Add(environmentId, ReadProcessorSettings(reader, environmentId));
                }
                settings.Processor.Environments.Add(platformId, environments);
            }

            return settings;
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
