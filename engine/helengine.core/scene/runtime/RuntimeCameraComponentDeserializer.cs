namespace helengine {
    /// <summary>
    /// Deserializes packaged camera components for player builds.
    /// </summary>
    public sealed class RuntimeCameraComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized camera component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for camera components.
        /// </summary>
        const string ComponentType = "helengine.CameraComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Camera component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{version}'.");
            }

            return new CameraComponent {
                CameraDrawOrder = reader.ReadByte(),
                LayerMask = reader.ReadUInt16(),
                Viewport = ReadFloat4(reader),
                ClearSettings = ReadClearSettings(reader)
            };
        }

        /// <summary>
        /// Reads one `float4` value from the camera payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the vector payload.</param>
        /// <returns>Decoded `float4` value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Reads one clear-settings payload from the camera record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the clear-settings payload.</param>
        /// <returns>Decoded camera clear settings.</returns>
        static CameraClearSettings ReadClearSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraClearSettings(
                reader.ReadByte() != 0,
                ReadFloat4(reader),
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadByte() != 0,
                reader.ReadByte());
        }
    }
}
