namespace helengine {
    /// <summary>
    /// Deserializes the built-in platform-info overlay binder for player builds without relying on runtime reflection.
    /// </summary>
    public sealed class RuntimePlatformInfoTextComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version shared by packaged automatic runtime component payloads.
        /// </summary>
        const byte CurrentPayloadVersion = 1;

        /// <inheritdoc />
        public string ComponentTypeId => PlatformInfoTextComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported platform-info text component payload version '{version}'.");
            }

            int memberCount = reader.ReadInt32();
            PlatformInfoTextComponent component = new PlatformInfoTextComponent();
            if (memberCount == 0) {
                return component;
            } else if (memberCount == 1) {
                component.UpdateOrder = reader.ReadByte();
                return component;
            }

            throw new InvalidOperationException(
                $"Platform-info text component expected 0 or 1 serialized members but payload contained {memberCount}.");
        }
    }
}
