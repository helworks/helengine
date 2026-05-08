namespace helengine.editor {
    /// <summary>
    /// Persists camera component render settings inside tolerant editor scene payloads.
    /// </summary>
    public class CameraComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Stable tagged field name used for camera draw order persistence.
        /// </summary>
        const string CameraDrawOrderFieldName = "CameraDrawOrder";

        /// <summary>
        /// Stable tagged field name used for camera layer-mask persistence.
        /// </summary>
        const string LayerMaskFieldName = "LayerMask";

        /// <summary>
        /// Stable tagged field name used for camera viewport persistence.
        /// </summary>
        const string ViewportFieldName = "Viewport";

        /// <summary>
        /// Stable tagged field name used for camera near clip-plane persistence.
        /// </summary>
        const string NearPlaneDistanceFieldName = "NearPlaneDistance";

        /// <summary>
        /// Stable tagged field name used for camera far clip-plane persistence.
        /// </summary>
        const string FarPlaneDistanceFieldName = "FarPlaneDistance";

        /// <summary>
        /// Stable tagged field name used for camera clear-settings persistence.
        /// </summary>
        const string ClearSettingsFieldName = "ClearSettings";

        /// <summary>
        /// Stable tagged field name used for camera render-settings persistence.
        /// </summary>
        const string RenderSettingsFieldName = "RenderSettings";

        /// <summary>
        /// Gets the concrete runtime component type handled by the descriptor.
        /// </summary>
        public Type ComponentType => typeof(CameraComponent);

        /// <summary>
        /// Gets the stable serialized type identifier written into scene files.
        /// </summary>
        public string ComponentTypeId => "helengine.CameraComponent";

        /// <summary>
        /// Serializes one live camera component into a scene component record.
        /// </summary>
        /// <param name="component">Live camera component instance to serialize.</param>
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
            if (component is not CameraComponent cameraComponent) {
                throw new InvalidOperationException("Camera component descriptor received an unsupported component type.");
            }

            byte cameraDrawOrder = cameraComponent.CameraDrawOrder;
            ushort layerMask = cameraComponent.LayerMask;
            float4 viewport = cameraComponent.Viewport;
            float nearPlaneDistance = cameraComponent.NearPlaneDistance;
            float farPlaneDistance = cameraComponent.FarPlaneDistance;
            CameraClearSettings clearSettings = cameraComponent.ClearSettings;
            CameraRenderSettings renderSettings = new CameraRenderSettings(cameraComponent.RenderSettings);
            EditorSceneCameraSuppressionComponent suppressionState = EditorSceneCameraSuppressionService.GetSuppressionState(cameraComponent);
            if (suppressionState != null) {
                cameraDrawOrder = suppressionState.CameraDrawOrder;
                layerMask = suppressionState.LayerMask;
                viewport = suppressionState.Viewport;
                nearPlaneDistance = suppressionState.NearPlaneDistance;
                farPlaneDistance = suppressionState.FarPlaneDistance;
                clearSettings = suppressionState.ClearSettings;
                renderSettings = new CameraRenderSettings(suppressionState.RenderSettings);
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(CameraDrawOrderFieldName, fieldWriter => fieldWriter.WriteByte(cameraDrawOrder));
            writer.WriteField(LayerMaskFieldName, fieldWriter => fieldWriter.WriteUInt16(layerMask));
            writer.WriteField(ViewportFieldName, fieldWriter => fieldWriter.WriteFloat4(viewport));
            writer.WriteField(NearPlaneDistanceFieldName, fieldWriter => fieldWriter.WriteSingle(nearPlaneDistance));
            writer.WriteField(FarPlaneDistanceFieldName, fieldWriter => fieldWriter.WriteSingle(farPlaneDistance));
            writer.WriteField(ClearSettingsFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(fieldWriter, clearSettings));
            writer.WriteField(RenderSettingsFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(fieldWriter, renderSettings));

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Deserializes one scene component record back into a live camera component instance.
        /// </summary>
        /// <param name="record">Serialized scene component record to materialize.</param>
        /// <param name="saveComponent">Hidden entity save component that should receive restored metadata.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime asset references.</param>
        /// <returns>Live camera component reconstructed from the scene record.</returns>
        public Component DeserializeComponent(
            SceneComponentAssetRecord record,
            EntitySaveComponent saveComponent,
            ISceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Camera component descriptor cannot deserialize '{record.ComponentTypeId}'.");
            }

            CameraComponent cameraComponent = new CameraComponent();
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());

            if (reader.TryGetFieldReader(CameraDrawOrderFieldName, out EngineBinaryReader cameraDrawOrderReader)) {
                using (cameraDrawOrderReader) {
                    cameraComponent.CameraDrawOrder = cameraDrawOrderReader.ReadByte();
                }
            }

            if (reader.TryGetFieldReader(LayerMaskFieldName, out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    cameraComponent.LayerMask = layerMaskReader.ReadUInt16();
                }
            }

            if (reader.TryGetFieldReader(ViewportFieldName, out EngineBinaryReader viewportReader)) {
                using (viewportReader) {
                    cameraComponent.Viewport = viewportReader.ReadFloat4();
                }
            }

            if (reader.TryGetFieldReader(NearPlaneDistanceFieldName, out EngineBinaryReader nearPlaneDistanceReader)) {
                using (nearPlaneDistanceReader) {
                    cameraComponent.NearPlaneDistance = nearPlaneDistanceReader.ReadSingle();
                }
            }

            if (reader.TryGetFieldReader(FarPlaneDistanceFieldName, out EngineBinaryReader farPlaneDistanceReader)) {
                using (farPlaneDistanceReader) {
                    cameraComponent.FarPlaneDistance = farPlaneDistanceReader.ReadSingle();
                }
            }

            if (reader.TryGetFieldReader(ClearSettingsFieldName, out EngineBinaryReader clearSettingsReader)) {
                using (clearSettingsReader) {
                    cameraComponent.ClearSettings = SceneComponentBinaryFieldEncoding.ReadCameraClearSettings(clearSettingsReader);
                }
            }

            if (reader.TryGetFieldReader(RenderSettingsFieldName, out EngineBinaryReader renderSettingsReader)) {
                using (renderSettingsReader) {
                    cameraComponent.RenderSettings = SceneComponentBinaryFieldEncoding.ReadCameraRenderSettings(renderSettingsReader);
                }
            }

            return cameraComponent;
        }
    }
}
