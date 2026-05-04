namespace helengine {
    /// <summary>
    /// Deserializes packaged 3D box collider components for player scene loading.
    /// </summary>
    public sealed class RuntimeBoxCollider3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized box-collider component scene records.
        /// </summary>
        const byte CurrentVersion = 2;

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

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1 && version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported box collider component payload version '{version}'.");
            }

            BoxCollider3DComponent component = new BoxCollider3DComponent {
                Size = reader.ReadFloat3()
            };
            if (version >= 2) {
                component.CollisionLayer = reader.ReadUInt16();
                component.CollisionMask = reader.ReadUInt16();
                component.IsTrigger = reader.ReadByte() != 0;
            }

            return component;
        }
    }
}
