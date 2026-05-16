namespace helengine {
    /// <summary>
    /// Deserializes the packaged demo-disc return-to-menu compatibility component without relying on runtime script reflection.
    /// </summary>
    public sealed class RuntimeDemoDiscReturnToMenuComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized component type id used by packaged demo-disc scenes.
        /// </summary>
        const string SerializedComponentTypeId = "city.menu.DemoDiscReturnToMenuComponent, gameplay";

        /// <summary>
        /// Current payload version shared by packaged automatic runtime component payloads.
        /// </summary>
        const byte CurrentPayloadVersion = 1;

        /// <inheritdoc />
        public string ComponentTypeId => SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported demo-disc return component payload version '{version}'.");
            }

            int memberCount = reader.ReadInt32();
            if (memberCount != 1) {
                throw new InvalidOperationException(
                    $"Demo-disc return component expected 1 serialized member but payload contained {memberCount}.");
            }

            reader.ReadByte();
            return new DemoDiscReturnToMenuRuntimeComponent();
        }
    }
}
