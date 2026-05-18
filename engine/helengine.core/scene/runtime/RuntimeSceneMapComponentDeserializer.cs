namespace helengine {
    /// <summary>
    /// Deserializes cooked scene-map component payloads for player builds.
    /// </summary>
    public sealed class RuntimeSceneMapComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => SceneMapComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != SceneMapComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported scene map component payload version '{version}'.");
            }

            int mappingCount = reader.ReadInt32();
            if (mappingCount < 0) {
                throw new InvalidOperationException("Scene map component payload mapping counts cannot be negative.");
            }

            SceneMapComponent component = new SceneMapComponent();
            for (int index = 0; index < mappingCount; index++) {
                string sourceSceneId = reader.ReadString();
                string targetSceneId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(sourceSceneId)) {
                    throw new InvalidOperationException("Scene map component payload entries must define a source scene id.");
                }
                if (string.IsNullOrWhiteSpace(targetSceneId)) {
                    throw new InvalidOperationException("Scene map component payload entries must define a target scene id.");
                }

                component.Mappings.Add(sourceSceneId, targetSceneId);
            }

            return component;
        }
    }
}
