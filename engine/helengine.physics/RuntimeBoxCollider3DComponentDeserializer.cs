namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D box collider components for runtime scene loading.
    /// </summary>
    public sealed class RuntimeBoxCollider3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Legacy payload version that only serialized the authored collider size.
        /// </summary>
        const byte LegacyVersion = 1;

        /// <summary>
        /// Current payload version for serialized box-collider component scene records.
        /// </summary>
        const byte CurrentVersion = 2;

        /// <summary>
        /// Automatic reflected box-collider payload member count used by unified built-in component persistence.
        /// </summary>
        const int AutomaticMemberCount = 7;

        /// <summary>
        /// Stable serialized component id for 3D box collider components.
        /// </summary>
        const string ComponentType = "helengine.BoxCollider3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Box collider component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == LegacyVersion && UsesAutomaticPayloadLayout(payload)) {
                reader.ReadInt32();
                return new BoxCollider3DComponent {
                    CollisionLayer = reader.ReadUInt16(),
                    CollisionMask = reader.ReadUInt16(),
                    DynamicFriction = reader.ReadDouble(),
                    IsTrigger = reader.ReadByte() != 0,
                    Restitution = reader.ReadDouble(),
                    Size = reader.ReadFloat3(),
                    StaticFriction = reader.ReadDouble()
                };
            }
            if (version != LegacyVersion && version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported box collider component payload version '{version}'.");
            }

            BoxCollider3DComponent component = new BoxCollider3DComponent {
                Size = reader.ReadFloat3()
            };
            if (version == LegacyVersion) {
                return component;
            }

            component.CollisionLayer = reader.ReadUInt16();
            component.CollisionMask = reader.ReadUInt16();
            component.IsTrigger = reader.ReadByte() != 0;
            return component;
        }

        /// <summary>
        /// Returns whether the supplied payload uses the automatic reflected box-collider layout instead of the legacy manual player layout.
        /// </summary>
        /// <param name="payload">Serialized box-collider payload to inspect.</param>
        /// <returns>True when the payload encodes the automatic reflected layout; otherwise false.</returns>
        static bool UsesAutomaticPayloadLayout(byte[] payload) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length < 5 || payload[0] != LegacyVersion) {
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
