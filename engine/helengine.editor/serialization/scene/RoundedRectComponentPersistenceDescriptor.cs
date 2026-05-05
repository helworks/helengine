namespace helengine.editor {
    /// <summary>
    /// Persists rounded rectangle scene visuals used by baked demo menus and other authored 2D chrome.
    /// </summary>
    public class RoundedRectComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized rounded rectangle records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(RoundedRectComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.RoundedRectComponent";

        /// <summary>
        /// Serializes one live rounded rectangle component into a scene record.
        /// </summary>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not RoundedRectComponent roundedRectComponent) {
                throw new InvalidOperationException("Rounded rectangle descriptor received an unsupported component type.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            writer.WriteByte(roundedRectComponent.RenderOrder2D);
            writer.WriteByte(roundedRectComponent.LayerMask);
            writer.WriteInt32((int)roundedRectComponent.Corners);
            writer.WriteSingle(roundedRectComponent.Rotation);
            WriteByte4(writer, roundedRectComponent.Color);
            writer.WriteFloat4(roundedRectComponent.SourceRect);
            writer.WriteInt2(roundedRectComponent.Size);
            writer.WriteSingle(roundedRectComponent.Radius);
            writer.WriteSingle(roundedRectComponent.BorderThickness);
            WriteByte4(writer, roundedRectComponent.FillColor);
            WriteByte4(writer, roundedRectComponent.BorderColor);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a live rounded rectangle component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported rounded rectangle component payload version '{version}'.");
            }

            return new RoundedRectComponent {
                RenderOrder2D = reader.ReadByte(),
                LayerMask = reader.ReadByte(),
                Corners = (RoundedRectCorners)reader.ReadInt32(),
                Rotation = reader.ReadSingle(),
                Color = ReadByte4(reader),
                SourceRect = reader.ReadFloat4(),
                Size = reader.ReadInt2(),
                Radius = reader.ReadSingle(),
                BorderThickness = reader.ReadSingle(),
                FillColor = ReadByte4(reader),
                BorderColor = ReadByte4(reader)
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
