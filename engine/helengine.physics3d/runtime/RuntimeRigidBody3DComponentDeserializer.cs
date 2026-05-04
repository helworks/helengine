namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D rigid-body components for player scene loading.
    /// </summary>
    public sealed class RuntimeRigidBody3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized rigid-body component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for 3D rigid-body components.
        /// </summary>
        const string ComponentType = "helengine.RigidBody3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Rigid body component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported rigid body component payload version '{version}'.");
            }

            RigidBody3DComponent component = new RigidBody3DComponent {
                BodyKind = (BodyKind3D)reader.ReadByte(),
                UseGravity = reader.ReadByte() != 0,
                Mass = reader.ReadSingle(),
                GravityScale = reader.ReadSingle(),
                LinearVelocity = reader.ReadFloat3()
            };
            return component;
        }
    }
}
