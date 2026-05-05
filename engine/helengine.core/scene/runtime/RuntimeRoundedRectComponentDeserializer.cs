namespace helengine {
    /// <summary>
    /// Deserializes rounded rectangle scene visuals for player builds.
    /// </summary>
    public sealed class RuntimeRoundedRectComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized type identifier written into scene files.
        /// </summary>
        const string ComponentType = "helengine.RoundedRectComponent";

        /// <summary>
        /// Current payload version for serialized rounded rectangle records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
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
        /// Reads one packed byte4 color from the payload.
        /// </summary>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
    }
}
