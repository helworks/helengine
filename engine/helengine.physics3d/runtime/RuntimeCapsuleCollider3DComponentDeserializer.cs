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

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported capsule collider component payload version '{version}'.");
            }

            CapsuleCollider3DComponent component = new CapsuleCollider3DComponent {
                Radius = reader.ReadSingle(),
                Height = reader.ReadSingle()
            };
            return component;
        }
    }
}
