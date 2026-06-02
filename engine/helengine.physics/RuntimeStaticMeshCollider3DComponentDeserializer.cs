namespace helengine {
    /// <summary>
    /// Deserializes packaged cooked static-mesh collider components for runtime scene loading.
    /// </summary>
    public sealed class RuntimeStaticMeshCollider3DComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized static-mesh collider component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for 3D static-mesh collider components.
        /// </summary>
        const string ComponentType = "helengine.StaticMeshCollider3DComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Static mesh collider component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported static mesh collider component payload version '{version}'.");
            }

            int vertexCount = reader.ReadInt32();
            float3[] vertices = new float3[vertexCount];
            for (int index = 0; index < vertexCount; index++) {
                vertices[index] = reader.ReadFloat3();
            }

            int indexCount = reader.ReadInt32();
            int[] indices = new int[indexCount];
            for (int index = 0; index < indexCount; index++) {
                indices[index] = reader.ReadInt32();
            }

            StaticMeshCollider3DComponent component = new StaticMeshCollider3DComponent {
                CollisionData = new StaticMeshCollisionData3D(vertices, indices)
            };
            return component;
        }
    }
}
