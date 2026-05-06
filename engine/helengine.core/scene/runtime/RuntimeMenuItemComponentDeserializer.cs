namespace helengine {
    /// <summary>
    /// Deserializes baked demo menu item metadata for player builds.
    /// </summary>
    public sealed class RuntimeMenuItemComponentDeserializer : IRuntimeComponentDeserializer {
        /// <inheritdoc />
        public string ComponentTypeId => MenuItemComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MenuItemComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported menu item component payload version '{version}'.");
            }

            return new MenuItemComponent {
                PanelId = reader.ReadString(),
                ItemId = reader.ReadString(),
                Description = reader.ReadString(),
                ActionKind = (MenuActionKind)reader.ReadByte(),
                TargetId = reader.ReadString(),
                IdleFillColor = ReadByte4(reader),
                IdleBorderColor = ReadByte4(reader),
                SelectedFillColor = ReadByte4(reader),
                SelectedBorderColor = ReadByte4(reader)
            };
        }

        /// <summary>
        /// Reads one packed byte4 color from the payload.
        /// </summary>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
    }
}
