namespace helengine {
    /// <summary>
    /// Deserializes packaged rounded-rectangle components for player builds.
    /// </summary>
    public sealed class RuntimeRoundedRectComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized rounded-rectangle scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for rounded-rectangle components.
        /// </summary>
        const string ComponentType = "helengine.RoundedRectComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Rounded rectangle component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

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
                SourceRect = ReadFloat4(reader),
                Size = reader.ReadInt2(),
                Radius = reader.ReadSingle(),
                BorderThickness = reader.ReadSingle(),
                FillColor = ReadByte4(reader),
                BorderColor = ReadByte4(reader)
            };
        }

        /// <summary>
        /// Reads one packed `byte4` color value from the rounded-rectangle payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the color payload.</param>
        /// <returns>Decoded `byte4` color value.</returns>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }

        /// <summary>
        /// Reads one `float4` value from the rounded-rectangle payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the vector payload.</param>
        /// <returns>Decoded `float4` value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
