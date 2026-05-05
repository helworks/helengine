namespace helengine.editor {
    /// <summary>
    /// Persists one baked demo menu item metadata component.
    /// </summary>
    public class DemoMenuItemComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(DemoMenuItemComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => DemoMenuItemComponent.SerializedComponentTypeId;

        /// <summary>
        /// Serializes one baked demo menu item metadata component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not DemoMenuItemComponent demoMenuItemComponent) {
                throw new InvalidOperationException("Demo menu item descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DemoMenuItemComponent.CurrentVersion);
            writer.WriteString(demoMenuItemComponent.PanelId);
            writer.WriteString(demoMenuItemComponent.ItemId);
            writer.WriteString(demoMenuItemComponent.Description);
            writer.WriteByte((byte)demoMenuItemComponent.ActionKind);
            writer.WriteString(demoMenuItemComponent.TargetId);
            WriteByte4(writer, demoMenuItemComponent.IdleFillColor);
            WriteByte4(writer, demoMenuItemComponent.IdleBorderColor);
            WriteByte4(writer, demoMenuItemComponent.SelectedFillColor);
            WriteByte4(writer, demoMenuItemComponent.SelectedBorderColor);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a baked demo menu item metadata component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != DemoMenuItemComponent.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported demo menu item component payload version '{version}'.");
            }

            return new DemoMenuItemComponent {
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
        /// Writes one packed byte4 color into the payload.
        /// </summary>
        static void WriteByte4(EngineBinaryWriter writer, byte4 value) {
            writer.WriteByte(value.X);
            writer.WriteByte(value.Y);
            writer.WriteByte(value.Z);
            writer.WriteByte(value.W);
        }

        /// <summary>
        /// Reads one packed byte4 color from the payload.
        /// </summary>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
    }
}
