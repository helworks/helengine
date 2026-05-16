namespace helengine.editor {
    /// <summary>
    /// Persists authored sprite component state and texture references inside tolerant editor scene payloads.
    /// </summary>
    public sealed class SpriteComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for sprite texture-reference persistence.
        /// </summary>
        const string TextureReferenceFieldName = "TextureReference";

        /// <summary>
        /// Stable tagged field name used for sprite source-rectangle persistence.
        /// </summary>
        const string SourceRectFieldName = "SourceRect";

        /// <summary>
        /// Stable tagged field name used for sprite size persistence.
        /// </summary>
        const string SizeFieldName = "Size";

        /// <summary>
        /// Stable tagged field name used for sprite color persistence.
        /// </summary>
        const string ColorFieldName = "Color";

        /// <summary>
        /// Stable tagged field name used for sprite rotation persistence.
        /// </summary>
        const string RotationFieldName = "Rotation";

        /// <summary>
        /// Stable tagged field name used for sprite 2D render-order persistence.
        /// </summary>
        const string RenderOrder2DFieldName = "RenderOrder2D";

        /// <summary>
        /// Stable tagged field name used for sprite layer-mask persistence.
        /// </summary>
        const string LayerMaskFieldName = "LayerMask";

        /// <inheritdoc />
        public Type ComponentType => typeof(SpriteComponent);

        /// <inheritdoc />
        public string ComponentTypeId => "helengine.SpriteComponent";

        /// <inheritdoc />
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (component is not SpriteComponent spriteComponent) {
                throw new InvalidOperationException("Sprite component descriptor received an unsupported component type.");
            }

            if (saveState == null) {
                throw new InvalidOperationException("SpriteComponent requires a texture asset reference before serialization.");
            }

            if (!saveState.TryGetAssetReference(TextureAssetScenePersistenceSupport.TextureReferenceName, out SceneAssetReference textureReference)) {
                throw new InvalidOperationException("SpriteComponent requires a texture asset reference before serialization.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(TextureReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, textureReference));
            writer.WriteField(SourceRectFieldName, fieldWriter => fieldWriter.WriteFloat4(spriteComponent.SourceRect));
            writer.WriteField(SizeFieldName, fieldWriter => fieldWriter.WriteInt2(spriteComponent.Size));
            writer.WriteField(ColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, spriteComponent.Color));
            writer.WriteField(RotationFieldName, fieldWriter => fieldWriter.WriteSingle(spriteComponent.Rotation));
            writer.WriteField(RenderOrder2DFieldName, fieldWriter => fieldWriter.WriteByte(spriteComponent.RenderOrder2D));
            writer.WriteField(LayerMaskFieldName, fieldWriter => fieldWriter.WriteByte(spriteComponent.LayerMask));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <inheritdoc />
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Sprite component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            try {
                return DeserializeTaggedComponent(record, saveComponent, referenceResolver);
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                if (TryDeserializeCookedRuntimeComponent(record, saveComponent, referenceResolver, out SpriteComponent cookedRuntimeComponent)) {
                    return cookedRuntimeComponent;
                }

                throw;
            }
        }

        /// <summary>
        /// Deserializes one authored tagged sprite payload back into a live runtime component.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live sprite component reconstructed from the tagged editor payload.</returns>
        SpriteComponent DeserializeTaggedComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            SpriteComponent spriteComponent = new SpriteComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(SourceRectFieldName, out EngineBinaryReader sourceRectReader)) {
                using (sourceRectReader) {
                    spriteComponent.SourceRect = sourceRectReader.ReadFloat4();
                }
            }
            if (reader.TryGetFieldReader(SizeFieldName, out EngineBinaryReader sizeReader)) {
                using (sizeReader) {
                    spriteComponent.Size = sizeReader.ReadInt2();
                }
            }
            if (reader.TryGetFieldReader(ColorFieldName, out EngineBinaryReader colorReader)) {
                using (colorReader) {
                    spriteComponent.Color = SceneComponentBinaryFieldEncoding.ReadByte4(colorReader);
                }
            }
            if (reader.TryGetFieldReader(RotationFieldName, out EngineBinaryReader rotationReader)) {
                using (rotationReader) {
                    spriteComponent.Rotation = rotationReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(RenderOrder2DFieldName, out EngineBinaryReader renderOrderReader)) {
                using (renderOrderReader) {
                    spriteComponent.RenderOrder2D = renderOrderReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(LayerMaskFieldName, out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    spriteComponent.LayerMask = layerMaskReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(TextureReferenceFieldName, out EngineBinaryReader textureReferenceReader)) {
                using (textureReferenceReader) {
                    SceneAssetReference textureReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(textureReferenceReader);
                    if (textureReference == null) {
                        throw new InvalidOperationException("SpriteComponent requires a texture asset reference before deserialization.");
                    }

                    spriteComponent.Texture = referenceResolver.ResolveTexture(textureReference);
                    if (saveComponent != null) {
                        saveComponent.SetAssetReference(spriteComponent, TextureAssetScenePersistenceSupport.TextureReferenceName, textureReference);
                    }
                }
            } else {
                throw new InvalidOperationException("SpriteComponent requires a texture asset reference before deserialization.");
            }

            return spriteComponent;
        }

        /// <summary>
        /// Attempts to deserialize one strict cooked-runtime sprite payload written by the scene packager.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <param name="spriteComponent">Cooked-runtime sprite component when deserialization succeeds.</param>
        /// <returns>True when the payload matched the cooked runtime layout; otherwise false.</returns>
        bool TryDeserializeCookedRuntimeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver,
            out SpriteComponent spriteComponent) {
            spriteComponent = new SpriteComponent();
            try {
                using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
                byte version = reader.ReadByte();
                if (version != 1) {
                    spriteComponent = null;
                    return false;
                }

                SceneAssetReference textureReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
                spriteComponent.SourceRect = reader.ReadFloat4();
                spriteComponent.Size = reader.ReadInt2();
                spriteComponent.Color = SceneComponentBinaryFieldEncoding.ReadByte4(reader);
                spriteComponent.Rotation = reader.ReadSingle();
                spriteComponent.RenderOrder2D = reader.ReadByte();
                spriteComponent.LayerMask = reader.ReadByte();

                if (textureReference == null) {
                    throw new InvalidOperationException("SpriteComponent requires a texture asset reference before deserialization.");
                }

                spriteComponent.Texture = referenceResolver.ResolveTexture(textureReference);
                if (saveComponent != null) {
                    saveComponent.SetAssetReference(spriteComponent, TextureAssetScenePersistenceSupport.TextureReferenceName, textureReference);
                }

                return true;
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                spriteComponent = null;
                return false;
            }
        }
    }
}
