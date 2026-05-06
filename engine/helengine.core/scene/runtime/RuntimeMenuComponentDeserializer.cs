namespace helengine {
    /// <summary>
    /// Deserializes baked demo menu root metadata for player builds.
    /// </summary>
    public sealed class RuntimeMenuComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => MenuComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MenuComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported menu component payload version '{version}'.");
            }

            return new MenuComponent {
                ProviderTypeName = reader.ReadString(),
                InitialPanelId = reader.ReadString()
            };
        }
    }
}
