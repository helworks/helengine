namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D capsule collider components for player scene loading.
    /// </summary>
    public sealed class RuntimeCapsuleCollider3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized capsule-collider component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Automatic reflected capsule-collider payload member count used by unified built-in component persistence.
        /// </summary>
        const int AutomaticMemberCount = 8;

        /// <summary>
        /// Stable serialized component id for 3D capsule collider components.
        /// </summary>
        const string ComponentType = "helengine.CapsuleCollider3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Capsule collider component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == CurrentVersion && UsesAutomaticPayloadLayout(payload)) {
                reader.ReadInt32();
                return new CapsuleCollider3DComponent {
                    CollisionLayer = reader.ReadUInt16(),
                    CollisionMask = reader.ReadUInt16(),
                    DynamicFriction = reader.ReadDouble(),
                    Height = reader.ReadSingle(),
                    IsTrigger = reader.ReadByte() != 0,
                    Radius = reader.ReadSingle(),
                    Restitution = reader.ReadDouble(),
                    StaticFriction = reader.ReadDouble()
                };
            }
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported capsule collider component payload version '{version}'.");
            }

            CapsuleCollider3DComponent component = new CapsuleCollider3DComponent {
                Radius = reader.ReadSingle(),
                Height = reader.ReadSingle()
            };
            return component;
        }

        /// <summary>
        /// Returns whether the supplied payload uses the automatic reflected capsule-collider layout instead of the legacy manual player layout.
        /// </summary>
        /// <param name="payload">Serialized capsule-collider payload to inspect.</param>
        /// <returns>True when the payload encodes the automatic reflected layout; otherwise false.</returns>
        static bool UsesAutomaticPayloadLayout(byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 5 || payload[0] != CurrentVersion) {
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
