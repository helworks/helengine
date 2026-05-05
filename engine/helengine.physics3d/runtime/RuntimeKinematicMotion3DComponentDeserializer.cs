namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D kinematic-motion components for player scene loading.
    /// </summary>
    public sealed class RuntimeKinematicMotion3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized kinematic-motion component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for 3D kinematic-motion components.
        /// </summary>
        const string ComponentType = "helengine.KinematicMotion3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Kinematic motion component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported kinematic motion component payload version '{version}'.");
            }

            KinematicMotion3DComponent component = new KinematicMotion3DComponent {
                StartLocalPosition = reader.ReadFloat3(),
                EndLocalPosition = reader.ReadFloat3(),
                TravelDurationSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                PingPong = reader.ReadByte() != 0
            };
            return component;
        }
    }
}
