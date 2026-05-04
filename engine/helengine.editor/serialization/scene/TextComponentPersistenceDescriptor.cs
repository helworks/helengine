namespace helengine.editor {
    /// <summary>
    /// Persists authored text component state and font references inside scene files.
    /// </summary>
    public class TextComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized text component records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(TextComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.TextComponent";

        /// <summary>
        /// Serializes one live text component into a scene component record.
        /// </summary>
        /// <param name="component">Live text component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (component is not TextComponent textComponent) {
                throw new InvalidOperationException("Text component descriptor received an unsupported component type.");
            }

            SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ResolveFontReference(nameof(TextComponent), textComponent.Font, saveState);
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            FontAssetScenePersistenceSupport.WriteOptionalReference(writer, fontReference);
            writer.WriteString(textComponent.Text);
            writer.WriteByte(textComponent.WrapText ? (byte)1 : (byte)0);
            writer.WriteInt2(textComponent.Size);
            FontAssetScenePersistenceSupport.WriteByte4(writer, textComponent.Color);
            writer.WriteFloat4(textComponent.SourceRect);
            writer.WriteSingle(textComponent.Rotation);
            writer.WriteByte(textComponent.RenderOrder2D);
            writer.WriteByte(textComponent.LayerMask);
            writer.WriteByte(textComponent.SelectionEnabled ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live text component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live text component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Text component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported text component payload version '{version}'.");
            }

            SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ReadOptionalReference(reader);
            TextComponent textComponent = new TextComponent {
                Text = reader.ReadString(),
                WrapText = reader.ReadByte() != 0,
                Size = reader.ReadInt2(),
                Color = FontAssetScenePersistenceSupport.ReadByte4(reader),
                SourceRect = reader.ReadFloat4(),
                Rotation = reader.ReadSingle(),
                RenderOrder2D = reader.ReadByte(),
                LayerMask = reader.ReadByte(),
                SelectionEnabled = reader.ReadByte() != 0
            };

            if (fontReference != null) {
                textComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(textComponent, FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
                }
            } else if (Core.Instance != null && Core.Instance.DefaultFontAsset != null) {
                textComponent.Font = Core.Instance.DefaultFontAsset;
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(textComponent, FontAssetScenePersistenceSupport.FontReferenceName, FontAssetScenePersistenceSupport.BuildEditorFontReference());
                }
            } else {
                throw new InvalidOperationException("TextComponent requires a font asset reference before deserialization.");
            }

            return textComponent;
        }
    }
}
