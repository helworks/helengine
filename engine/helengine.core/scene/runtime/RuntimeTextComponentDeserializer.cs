namespace helengine {
    /// <summary>
    /// Deserializes text scene visuals for player builds.
    /// </summary>
    public sealed class RuntimeTextComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Stable serialized type identifier written into scene files.
        /// </summary>
        const string ComponentType = "helengine.TextComponent";

        /// <summary>
        /// Current payload version for serialized text component records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            referenceResolver.LastTextLoadStage = "DeserializeBegin";
            referenceResolver.LastTextFontRelativePath = string.Empty;
            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported text component payload version '{version}'.");
            }

            referenceResolver.LastTextLoadStage = "BeforeReadFontReference";
            SceneAssetReference fontReference = ReadOptionalReference(reader);
            referenceResolver.LastTextLoadStage = "AfterReadFontReference";
            referenceResolver.LastTextFontRelativePath = fontReference != null ? fontReference.RelativePath : string.Empty;
            referenceResolver.LastTextLoadStage = "BeforeConstructTextComponent";
            TextComponent textComponent = new TextComponent {
                Text = reader.ReadString(),
                WrapText = reader.ReadByte() != 0,
                Size = reader.ReadInt2(),
                Color = ReadByte4(reader),
                SourceRect = reader.ReadFloat4(),
                Rotation = reader.ReadSingle(),
                RenderOrder2D = reader.ReadByte(),
                LayerMask = reader.ReadByte(),
                SelectionEnabled = reader.ReadByte() != 0
            };
            if (fontReference == null) {
                throw new InvalidOperationException("TextComponent requires a packaged font reference before deserialization.");
            }

            referenceResolver.LastTextLoadStage = "BeforeResolveFont";
            textComponent.Font = referenceResolver.ResolveFont(fontReference);
            referenceResolver.LastTextLoadStage = "AfterResolveFont";
            return textComponent;
        }

        /// <summary>
        /// Reads one optional scene asset reference from the component payload.
        /// </summary>
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
        static byte4 ReadByte4(EngineBinaryReader reader) {
            return new byte4(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }
    }
}
