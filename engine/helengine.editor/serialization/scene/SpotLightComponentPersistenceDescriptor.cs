namespace helengine.editor {
    /// <summary>
    /// Persists spot light component authored values inside tolerant editor scene payloads.
    /// </summary>
    public class SpotLightComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for spot light range persistence.
        /// </summary>
        const string RangeFieldName = "Range";

        /// <summary>
        /// Stable tagged field name used for spot light inner-cone persistence.
        /// </summary>
        const string InnerConeAngleDegreesFieldName = "InnerConeAngleDegrees";

        /// <summary>
        /// Stable tagged field name used for spot light outer-cone persistence.
        /// </summary>
        const string OuterConeAngleDegreesFieldName = "OuterConeAngleDegrees";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(SpotLightComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.SpotLightComponent";

        /// <summary>
        /// Serializes one live spot light into a scene component record.
        /// </summary>
        /// <param name="component">Live spot light instance to serialize.</param>
        /// <param name="componentIndex">Entity-local index used to preserve component ordering.</param>
        /// <param name="saveState">Editor-time save metadata associated with the component.</param>
        /// <returns>Serialized scene component record.</returns>
        public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            } else if (component is not SpotLightComponent) {
                throw new InvalidOperationException("Spot light descriptor received an unsupported component type.");
            }

            SpotLightComponent lightComponent = (SpotLightComponent)component;
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField(RangeFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.Range));
            writer.WriteField(InnerConeAngleDegreesFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.InnerConeAngleDegrees));
            writer.WriteField(OuterConeAngleDegreesFieldName, fieldWriter => fieldWriter.WriteSingle(lightComponent.OuterConeAngleDegrees));
            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live spot light instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live spot light reconstructed from the scene record.</returns>
        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            } else if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Spot light descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            SpotLightComponent lightComponent = new SpotLightComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            LightComponentTaggedFieldEncoding.ReadCommonFields(reader, lightComponent);
            if (reader.TryGetFieldReader(RangeFieldName, out EngineBinaryReader rangeReader)) {
                using (rangeReader) {
                    lightComponent.Range = rangeReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(InnerConeAngleDegreesFieldName, out EngineBinaryReader innerConeAngleDegreesReader)) {
                using (innerConeAngleDegreesReader) {
                    lightComponent.InnerConeAngleDegrees = innerConeAngleDegreesReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader(OuterConeAngleDegreesFieldName, out EngineBinaryReader outerConeAngleDegreesReader)) {
                using (outerConeAngleDegreesReader) {
                    lightComponent.OuterConeAngleDegrees = outerConeAngleDegreesReader.ReadSingle();
                }
            }

            return lightComponent;
        }
    }
}
