namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D rigid-body components for runtime scene loading.
    /// </summary>
    public sealed class RuntimeRigidBody3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized rigid-body component scene records.
        /// </summary>
        const byte CurrentVersion = 2;

        /// <summary>
        /// Automatic reflected rigid-body payload member count used by unified built-in component persistence.
        /// </summary>
        const int AutomaticMemberCount = 6;

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

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == 1 && UsesAutomaticPayloadLayout(payload)) {
                reader.ReadInt32();
                return new RigidBody3DComponent {
                    AngularVelocity = reader.ReadFloat3(),
                    BodyKind = (BodyKind3D)reader.ReadInt32(),
                    GravityScale = reader.ReadDouble(),
                    LinearVelocity = reader.ReadFloat3(),
                    Mass = reader.ReadDouble(),
                    UseGravity = reader.ReadByte() != 0
                };
            }
            if (version != 1 && version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported rigid body component payload version '{version}'.");
            }

            RigidBody3DComponent component = new RigidBody3DComponent {
                BodyKind = (BodyKind3D)reader.ReadByte(),
                UseGravity = reader.ReadByte() != 0,
                Mass = reader.ReadSingle(),
                GravityScale = reader.ReadSingle(),
                LinearVelocity = reader.ReadFloat3()
            };
            if (version >= 2) {
                component.AngularVelocity = reader.ReadFloat3();
            }

            return component;
        }

        /// <summary>
        /// Returns whether the supplied payload uses the automatic reflected rigid-body layout instead of the legacy manual player layout.
        /// </summary>
        /// <param name="payload">Serialized rigid-body payload to inspect.</param>
        /// <returns>True when the payload encodes the automatic reflected layout; otherwise false.</returns>
        static bool UsesAutomaticPayloadLayout(byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 5 || payload[0] != 1) {
                return false;
            }

            int memberCount =
                payload[1]
                | (payload[2] << 8)
                | (payload[3] << 16)
                | (payload[4] << 24);
            return memberCount == AutomaticMemberCount;
        }
    }
}
