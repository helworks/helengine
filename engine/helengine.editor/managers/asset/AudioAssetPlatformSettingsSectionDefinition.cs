namespace helengine.editor {
    /// <summary>
    /// Defines serialization and comparison behavior for the registered audio section.
    /// </summary>
    public sealed class AudioAssetPlatformSettingsSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the stable section identifier.
        /// </summary>
        public const string SectionIdValue = "audio";

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId => SectionIdValue;

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        public Type SettingsType => typeof(AudioAssetProcessorSettings);

        /// <summary>
        /// Creates one default audio settings payload.
        /// </summary>
        /// <returns>Default audio settings payload.</returns>
        public object CreateDefaultSettings() {
            return new AudioAssetProcessorSettings();
        }

        /// <summary>
        /// Creates one deep clone of the supplied audio settings payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned audio settings payload.</returns>
        public object CloneSettings(object settings) {
            AudioAssetProcessorSettings source = RequireSettings(settings);
            return new AudioAssetProcessorSettings {
                EncodingFamilyId = source.EncodingFamilyId ?? string.Empty,
                PlaybackMode = source.PlaybackMode,
                TargetChannels = source.TargetChannels,
                TargetSampleRate = source.TargetSampleRate,
                StreamChunkByteSize = source.StreamChunkByteSize,
                DefaultLoop = source.DefaultLoop,
                DefaultBusId = source.DefaultBusId ?? string.Empty
            };
        }

        /// <summary>
        /// Returns whether two audio settings payloads carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both audio payloads match.</returns>
        public bool SettingsEqual(object left, object right) {
            AudioAssetProcessorSettings leftSettings = RequireSettings(left);
            AudioAssetProcessorSettings rightSettings = RequireSettings(right);
            return string.Equals(leftSettings.EncodingFamilyId ?? string.Empty, rightSettings.EncodingFamilyId ?? string.Empty, StringComparison.Ordinal)
                && leftSettings.PlaybackMode == rightSettings.PlaybackMode
                && leftSettings.TargetChannels == rightSettings.TargetChannels
                && leftSettings.TargetSampleRate == rightSettings.TargetSampleRate
                && leftSettings.StreamChunkByteSize == rightSettings.StreamChunkByteSize
                && leftSettings.DefaultLoop == rightSettings.DefaultLoop
                && string.Equals(leftSettings.DefaultBusId ?? string.Empty, rightSettings.DefaultBusId ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Serializes one audio settings payload.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            AudioAssetProcessorSettings audioSettings = RequireSettings(settings);
            ValidateSettings(audioSettings);
            writer.WriteString(audioSettings.EncodingFamilyId);
            writer.WriteByte((byte)audioSettings.PlaybackMode);
            writer.WriteUInt16(audioSettings.TargetChannels);
            writer.WriteInt32(audioSettings.TargetSampleRate);
            writer.WriteInt32(audioSettings.StreamChunkByteSize);
            writer.WriteByte(audioSettings.DefaultLoop ? (byte)1 : (byte)0);
            writer.WriteString(audioSettings.DefaultBusId);
        }

        /// <summary>
        /// Deserializes one audio settings payload.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized audio settings payload.</returns>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            AudioAssetProcessorSettings audioSettings = new AudioAssetProcessorSettings {
                EncodingFamilyId = reader.ReadString(),
                PlaybackMode = (AudioPlaybackMode)reader.ReadByte(),
                TargetChannels = reader.ReadUInt16(),
                TargetSampleRate = reader.ReadInt32(),
                StreamChunkByteSize = reader.ReadInt32(),
                DefaultLoop = reader.ReadByte() != 0,
                DefaultBusId = reader.ReadString()
            };
            ValidateSettings(audioSettings);
            return audioSettings;
        }

        /// <summary>
        /// Validates that the supplied payload is one audio settings instance.
        /// </summary>
        /// <param name="settings">Payload instance to validate.</param>
        /// <returns>Validated audio settings payload.</returns>
        AudioAssetProcessorSettings RequireSettings(object settings) {
            if (settings is not AudioAssetProcessorSettings audioSettings) {
                throw new InvalidOperationException($"Section '{SectionIdValue}' requires one {nameof(AudioAssetProcessorSettings)} payload.");
            }

            return audioSettings;
        }

        /// <summary>
        /// Validates one audio settings payload before serialization or after deserialization.
        /// </summary>
        /// <param name="settings">Settings payload to validate.</param>
        static void ValidateSettings(AudioAssetProcessorSettings settings) {
            if (string.IsNullOrWhiteSpace(settings.EncodingFamilyId)) {
                throw new InvalidOperationException("Audio encoding family id must be provided.");
            }
            if (settings.TargetSampleRate < 0) {
                throw new InvalidOperationException("Audio target sample rate cannot be negative.");
            }
            if (settings.StreamChunkByteSize < 0) {
                throw new InvalidOperationException("Audio stream chunk size cannot be negative.");
            }
            if (string.IsNullOrWhiteSpace(settings.DefaultBusId)) {
                throw new InvalidOperationException("Audio default bus id must be provided.");
            }
        }
    }
}
