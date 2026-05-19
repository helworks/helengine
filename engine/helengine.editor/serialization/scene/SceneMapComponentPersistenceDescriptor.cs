namespace helengine.editor {
    /// <summary>
    /// Persists authored scene-map component entries and accepts the cooked runtime payload shape used by packaged builds.
    /// </summary>
    public sealed class SceneMapComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for the optional authored initial scene id.
        /// </summary>
        const string InitialSceneIdFieldName = "InitialSceneId";

        /// <summary>
        /// Stable tagged field name used for the mapping count.
        /// </summary>
        const string MappingCountFieldName = "MappingCount";

        /// <summary>
        /// Stable prefix used for tagged mapping source field names.
        /// </summary>
        const string MappingSourceFieldNamePrefix = "MappingSource";

        /// <summary>
        /// Stable prefix used for tagged mapping target field names.
        /// </summary>
        const string MappingTargetFieldNamePrefix = "MappingTarget";

        /// <inheritdoc />
        public Type ComponentType => typeof(SceneMapComponent);

        /// <inheritdoc />
        public string ComponentTypeId => SceneMapComponent.SerializedComponentTypeId;

        /// <inheritdoc />
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component is not SceneMapComponent sceneMapComponent) {
                throw new InvalidOperationException("Scene map descriptor received an unsupported component type.");
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            List<KeyValuePair<string, string>> mappings = sceneMapComponent.Mappings.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            writer.WriteField(InitialSceneIdFieldName, fieldWriter => fieldWriter.WriteString(sceneMapComponent.InitialSceneId ?? string.Empty));
            writer.WriteField(MappingCountFieldName, fieldWriter => fieldWriter.WriteInt32(mappings.Count));
            for (int index = 0; index < mappings.Count; index++) {
                KeyValuePair<string, string> mapping = mappings[index];
                writer.WriteField(MappingSourceFieldNamePrefix + index, fieldWriter => fieldWriter.WriteString(mapping.Key));
                writer.WriteField(MappingTargetFieldNamePrefix + index, fieldWriter => fieldWriter.WriteString(mapping.Value));
            }

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
                throw new InvalidOperationException($"Scene map descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            try {
                return DeserializeTaggedComponent(record);
            } catch (Exception ex) when (ex is InvalidOperationException || ex is EndOfStreamException) {
                return DeserializeCookedRuntimeComponent(record);
            }
        }

        /// <summary>
        /// Deserializes one tolerant tagged editor payload into a scene-map component.
        /// </summary>
        /// <param name="record">Serialized scene component record.</param>
        /// <returns>Loaded scene-map component.</returns>
        SceneMapComponent DeserializeTaggedComponent(SceneComponentAssetRecord record) {
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            if (!reader.TryGetFieldReader(MappingCountFieldName, out EngineBinaryReader mappingCountReader)) {
                throw new InvalidOperationException("Scene map tagged payload is missing the mapping count field.");
            }

            using (mappingCountReader) {
                int mappingCount = mappingCountReader.ReadInt32();
                if (mappingCount < 0) {
                    throw new InvalidOperationException("Scene map tagged payload mapping counts cannot be negative.");
                }

                SceneMapComponent component = new SceneMapComponent();
                if (reader.TryGetFieldReader(InitialSceneIdFieldName, out EngineBinaryReader initialSceneIdReader)) {
                    using (initialSceneIdReader) {
                        component.InitialSceneId = initialSceneIdReader.ReadString() ?? string.Empty;
                    }
                }

                for (int index = 0; index < mappingCount; index++) {
                    string sourceSceneId = ReadRequiredTaggedField(reader, MappingSourceFieldNamePrefix + index);
                    string targetSceneId = ReadRequiredTaggedField(reader, MappingTargetFieldNamePrefix + index);
                    component.Mappings.Add(sourceSceneId, targetSceneId);
                }

                return component;
            }
        }

        /// <summary>
        /// Deserializes one strict cooked runtime payload into a scene-map component.
        /// </summary>
        /// <param name="record">Serialized scene component record.</param>
        /// <returns>Loaded scene-map component.</returns>
        SceneMapComponent DeserializeCookedRuntimeComponent(SceneComponentAssetRecord record) {
            RuntimeSceneMapComponentDeserializer deserializer = new RuntimeSceneMapComponentDeserializer();
            return (SceneMapComponent)deserializer.Deserialize(record, null);
        }

        /// <summary>
        /// Reads one required tagged string field from the supplied payload reader.
        /// </summary>
        /// <param name="reader">Tagged payload reader that owns the field.</param>
        /// <param name="fieldName">Stable field name to read.</param>
        /// <returns>Decoded string field value.</returns>
        string ReadRequiredTaggedField(EditorTaggedSceneComponentFieldReader reader, string fieldName) {
            if (!reader.TryGetFieldReader(fieldName, out EngineBinaryReader fieldReader)) {
                throw new InvalidOperationException($"Scene map tagged payload is missing required field '{fieldName}'.");
            }

            using (fieldReader) {
                string value = fieldReader.ReadString();
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new InvalidOperationException($"Scene map tagged payload field '{fieldName}' cannot be empty.");
                }

                return value;
            }
        }
    }
}
