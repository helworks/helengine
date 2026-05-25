namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D sphere collider components for player scene loading.
    /// </summary>
    public sealed class RuntimeSphereCollider3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized sphere-collider component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Automatic reflected sphere-collider payload member count used by unified built-in component persistence.
        /// </summary>
        const int AutomaticMemberCount = 7;

        /// <summary>
        /// Stable serialized component id for 3D sphere collider components.
        /// </summary>
        const string ComponentType = "helengine.SphereCollider3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Sphere collider component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            using MemoryStream stream = new MemoryStream(payload, false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version == CurrentVersion && UsesAutomaticPayloadLayout(payload)) {
                reader.ReadInt32();
                return new SphereCollider3DComponent {
                    CollisionLayer = reader.ReadUInt16(),
                    CollisionMask = reader.ReadUInt16(),
                    DynamicFriction = reader.ReadDouble(),
                    IsTrigger = reader.ReadByte() != 0,
                    Radius = reader.ReadSingle(),
                    Restitution = reader.ReadDouble(),
                    StaticFriction = reader.ReadDouble()
                };
            }
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported sphere collider component payload version '{version}'.");
            }

            SphereCollider3DComponent component = new SphereCollider3DComponent {
                Radius = reader.ReadSingle()
            };
            return component;
        }

        /// <summary>
        /// Returns whether the supplied payload uses the automatic reflected sphere-collider layout instead of the legacy manual player layout.
        /// </summary>
        /// <param name="payload">Serialized sphere-collider payload to inspect.</param>
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
