namespace helengine {
    /// <summary>
    /// Deserializes sprite scene visuals for player builds.
    /// </summary>
    public sealed class RuntimeSpriteComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized type identifier written into scene files.
        /// </summary>
        const string ComponentType = "helengine.SpriteComponent";

        /// <summary>
        /// Current payload version for serialized sprite component records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Sprite component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported sprite component payload version '{version}'.");
            }

            SceneAssetReference textureReference = ReadOptionalReference(reader);
            if (textureReference == null) {
                throw new InvalidOperationException("SpriteComponent requires a packaged texture reference before deserialization.");
            }

            return new SpriteComponent {
                Texture = referenceResolver.ResolveTexture(textureReference),
                SourceRect = reader.ReadFloat4(),
                Size = reader.ReadInt2(),
                Color = ReadByte4(reader),
                Rotation = reader.ReadSingle(),
                RenderOrder2D = reader.ReadByte(),
                LayerMask = reader.ReadByte()
            };
        }

        /// <summary>
        /// Reads one optional scene asset reference from the component payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the reference field.</param>
        /// <returns>Decoded scene asset reference, or null when absent.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }

        /// <summary>
        /// Reads one packed byte4 color from the payload.
        /// </summary>
        /// <param name="reader">Binary reader positioned at the color field.</param>
        /// <returns>Decoded color value.</returns>
        static byte4 ReadByte4(EngineBinaryReader reader) {
            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
    }
}
