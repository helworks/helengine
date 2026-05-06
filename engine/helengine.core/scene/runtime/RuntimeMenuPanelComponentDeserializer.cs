namespace helengine {
    /// <summary>
    /// Deserializes baked demo menu panel metadata for player builds.
    /// </summary>
    public sealed class RuntimeMenuPanelComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => MenuPanelComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MenuPanelComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported menu panel component payload version '{version}'.");
            }

            return new MenuPanelComponent {
                PanelId = reader.ReadString()
            };
        }
    }
}
