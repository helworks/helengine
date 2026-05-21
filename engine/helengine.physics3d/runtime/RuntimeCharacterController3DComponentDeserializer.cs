namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D character-controller components for player scene loading.
    /// </summary>
    public sealed class RuntimeCharacterController3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized character-controller component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for 3D character-controller components.
        /// </summary>
        const string ComponentType = "helengine.CharacterController3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Character controller component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported character controller component payload version '{version}'.");
            }

            return new CharacterController3DComponent {
                DesiredMoveDirection = reader.ReadFloat3(),
                MoveSpeed = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                GravityScale = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                StepHeight = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                GroundSnapDistance = BitConverter.Int64BitsToDouble(reader.ReadInt64())
            };
        }
    }
}
