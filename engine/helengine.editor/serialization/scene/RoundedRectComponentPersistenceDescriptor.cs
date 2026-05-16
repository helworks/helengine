namespace helengine.editor {
    /// <summary>
    /// Persists rounded rectangle scene visuals inside tolerant editor scene payloads.
    /// </summary>
    public class RoundedRectComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for rounded-rectangle 2D render-order persistence.
        /// </summary>
        const string RenderOrder2DFieldName = "RenderOrder2D";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle layer-mask persistence.
        /// </summary>
        const string LayerMaskFieldName = "LayerMask";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle corner-mask persistence.
        /// </summary>
        const string CornersFieldName = "Corners";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle rotation persistence.
        /// </summary>
        const string RotationFieldName = "Rotation";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle color persistence.
        /// </summary>
        const string ColorFieldName = "Color";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle source-rectangle persistence.
        /// </summary>
        const string SourceRectFieldName = "SourceRect";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle size persistence.
        /// </summary>
        const string SizeFieldName = "Size";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle radius persistence.
        /// </summary>
        const string RadiusFieldName = "Radius";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle border-thickness persistence.
        /// </summary>
        const string BorderThicknessFieldName = "BorderThickness";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle fill-color persistence.
        /// </summary>
        const string FillColorFieldName = "FillColor";

        /// <summary>
        /// Stable tagged field name used for rounded-rectangle border-color persistence.
        /// </summary>
        const string BorderColorFieldName = "BorderColor";

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

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(RenderOrder2DFieldName, fieldWriter => fieldWriter.WriteByte(roundedRectComponent.RenderOrder2D));
            writer.WriteField(LayerMaskFieldName, fieldWriter => fieldWriter.WriteByte(roundedRectComponent.LayerMask));
            writer.WriteField(CornersFieldName, fieldWriter => fieldWriter.WriteInt32((int)roundedRectComponent.Corners));
            writer.WriteField(RotationFieldName, fieldWriter => fieldWriter.WriteSingle(roundedRectComponent.Rotation));
            writer.WriteField(ColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, roundedRectComponent.Color));
            writer.WriteField(SourceRectFieldName, fieldWriter => fieldWriter.WriteFloat4(roundedRectComponent.SourceRect));
            writer.WriteField(SizeFieldName, fieldWriter => fieldWriter.WriteInt2(roundedRectComponent.Size));
            writer.WriteField(RadiusFieldName, fieldWriter => fieldWriter.WriteSingle(roundedRectComponent.Radius));
            writer.WriteField(BorderThicknessFieldName, fieldWriter => fieldWriter.WriteSingle(roundedRectComponent.BorderThickness));
            writer.WriteField(FillColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, roundedRectComponent.FillColor));
            writer.WriteField(BorderColorFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteByte4(fieldWriter, roundedRectComponent.BorderColor));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene record back into a live rounded rectangle component.
        /// </summary>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Rounded rectangle descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            try {
                return DeserializeTaggedComponent(record);
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                if (TryDeserializeCookedRuntimeComponent(record, out RoundedRectComponent cookedRuntimeComponent)) {
                    return cookedRuntimeComponent;
                }

                throw;
            }
        }

        /// <summary>
        /// Deserializes one authored tagged rounded-rectangle payload back into a live runtime component.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <returns>Live rounded-rectangle component reconstructed from the tagged editor payload.</returns>
        RoundedRectComponent DeserializeTaggedComponent(SceneComponentAssetRecord record) {
            RoundedRectComponent roundedRectComponent = new RoundedRectComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (reader.TryGetFieldReader(RenderOrder2DFieldName, out EngineBinaryReader renderOrder2DReader)) {
                using (renderOrder2DReader) {
                    roundedRectComponent.RenderOrder2D = renderOrder2DReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(LayerMaskFieldName, out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    roundedRectComponent.LayerMask = layerMaskReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader(CornersFieldName, out EngineBinaryReader cornersReader)) {
                using (cornersReader) {
                    roundedRectComponent.Corners = (RoundedRectCorners)cornersReader.ReadInt32();
                }
            }
            if (reader.TryGetFieldReader(RotationFieldName, out EngineBinaryReader rotationReader)) {
                using (rotationReader) {
                    roundedRectComponent.Rotation = rotationReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(ColorFieldName, out EngineBinaryReader colorReader)) {
                using (colorReader) {
                    roundedRectComponent.Color = SceneComponentBinaryFieldEncoding.ReadByte4(colorReader);
                }
            }
            if (reader.TryGetFieldReader(SourceRectFieldName, out EngineBinaryReader sourceRectReader)) {
                using (sourceRectReader) {
                    roundedRectComponent.SourceRect = sourceRectReader.ReadFloat4();
                }
            }
            if (reader.TryGetFieldReader(SizeFieldName, out EngineBinaryReader sizeReader)) {
                using (sizeReader) {
                    roundedRectComponent.Size = sizeReader.ReadInt2();
                }
            }
            if (reader.TryGetFieldReader(RadiusFieldName, out EngineBinaryReader radiusReader)) {
                using (radiusReader) {
                    roundedRectComponent.Radius = radiusReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(BorderThicknessFieldName, out EngineBinaryReader borderThicknessReader)) {
                using (borderThicknessReader) {
                    roundedRectComponent.BorderThickness = borderThicknessReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(FillColorFieldName, out EngineBinaryReader fillColorReader)) {
                using (fillColorReader) {
                    roundedRectComponent.FillColor = SceneComponentBinaryFieldEncoding.ReadByte4(fillColorReader);
                }
            }
            if (reader.TryGetFieldReader(BorderColorFieldName, out EngineBinaryReader borderColorReader)) {
                using (borderColorReader) {
                    roundedRectComponent.BorderColor = SceneComponentBinaryFieldEncoding.ReadByte4(borderColorReader);
                }
            }

            return roundedRectComponent;
        }

        /// <summary>
        /// Attempts to deserialize one strict cooked-runtime rounded-rectangle payload written by the scene packager.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="roundedRectComponent">Cooked-runtime rounded-rectangle component when deserialization succeeds.</param>
        /// <returns>True when the payload matched the cooked runtime layout; otherwise false.</returns>
        bool TryDeserializeCookedRuntimeComponent(SceneComponentAssetRecord record, out RoundedRectComponent roundedRectComponent) {
            roundedRectComponent = new RoundedRectComponent();
            try {
                using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
                using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
                byte version = reader.ReadByte();
                if (version != 1) {
                    roundedRectComponent = null;
                    return false;
                }

                roundedRectComponent.RenderOrder2D = reader.ReadByte();
                roundedRectComponent.LayerMask = reader.ReadByte();
                roundedRectComponent.Corners = (RoundedRectCorners)reader.ReadInt32();
                roundedRectComponent.Rotation = reader.ReadSingle();
                roundedRectComponent.Color = SceneComponentBinaryFieldEncoding.ReadByte4(reader);
                roundedRectComponent.SourceRect = reader.ReadFloat4();
                roundedRectComponent.Size = reader.ReadInt2();
                roundedRectComponent.Radius = reader.ReadSingle();
                roundedRectComponent.BorderThickness = reader.ReadSingle();
                roundedRectComponent.FillColor = SceneComponentBinaryFieldEncoding.ReadByte4(reader);
                roundedRectComponent.BorderColor = SceneComponentBinaryFieldEncoding.ReadByte4(reader);
                return true;
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                roundedRectComponent = null;
                return false;
            }
        }
    }
}
