namespace helengine.editor {
    /// <summary>
    /// Persists camera component render settings inside scene files.
    /// </summary>
    public class CameraComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        /// <summary>
        /// Current payload version for serialized camera component records.
        /// </summary>
        const byte CurrentVersion = 2;

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
            CameraClearSettings clearSettings = cameraComponent.ClearSettings;
            CameraRenderSettings renderSettings = new CameraRenderSettings(cameraComponent.RenderSettings);
            EditorSceneCameraSuppressionComponent suppressionState = EditorSceneCameraSuppressionService.GetSuppressionState(cameraComponent);
            if (suppressionState != null) {
                cameraDrawOrder = suppressionState.CameraDrawOrder;
                layerMask = suppressionState.LayerMask;
                viewport = suppressionState.Viewport;
                clearSettings = suppressionState.ClearSettings;
                renderSettings = new CameraRenderSettings(suppressionState.RenderSettings);
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CurrentVersion);
            writer.WriteByte(cameraDrawOrder);
            writer.WriteUInt16(layerMask);
            WriteFloat4(writer, viewport);
            WriteClearSettings(writer, clearSettings);
            WriteRenderSettings(writer, renderSettings);

            return new SceneComponentAssetRecord {
                ComponentTypeId = ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
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

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1 && version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{version}'.");
            }

            CameraComponent cameraComponent = new CameraComponent {
                CameraDrawOrder = reader.ReadByte(),
                LayerMask = reader.ReadUInt16(),
                Viewport = ReadFloat4(reader),
                ClearSettings = ReadClearSettings(reader)
            };
            if (version >= 2) {
                cameraComponent.RenderSettings = ReadRenderSettings(reader);
            }

            return cameraComponent;
        }

        /// <summary>
        /// Writes one `float4` value into the camera payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="value">Vector value to write.</param>
        static void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads one `float4` value from the camera payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the vector payload.</param>
        /// <returns>Decoded `float4` value.</returns>
        static float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Writes one clear-settings payload into the camera record.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="settings">Clear settings to serialize.</param>
        static void WriteClearSettings(EngineBinaryWriter writer, CameraClearSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(settings.ClearColorEnabled ? (byte)1 : (byte)0);
            WriteFloat4(writer, settings.ClearColor);
            writer.WriteByte(settings.ClearDepthEnabled ? (byte)1 : (byte)0);
            writer.WriteSingle(settings.ClearDepth);
            writer.WriteByte(settings.ClearStencilEnabled ? (byte)1 : (byte)0);
            writer.WriteByte(settings.ClearStencil);
        }

        /// <summary>
        /// Reads one clear-settings payload from the camera record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the clear-settings payload.</param>
        /// <returns>Decoded camera clear settings.</returns>
        static CameraClearSettings ReadClearSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraClearSettings(
                reader.ReadByte() != 0,
                ReadFloat4(reader),
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadByte() != 0,
                reader.ReadByte());
        }

        /// <summary>
        /// Writes one render-settings payload into the camera record.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="settings">Render settings to serialize.</param>
        static void WriteRenderSettings(EngineBinaryWriter writer, CameraRenderSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            writer.WriteByte((byte)settings.DepthPrepassMode);
            writer.WriteSingle(settings.ShadowDistance);
            writer.WriteByte((byte)settings.PostProcessTier);
        }

        /// <summary>
        /// Reads one render-settings payload from the camera record.
        /// </summary>
        /// <param name="reader">Source reader positioned at the render-settings payload.</param>
        /// <returns>Decoded camera render settings.</returns>
        static CameraRenderSettings ReadRenderSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraRenderSettings {
                DepthPrepassMode = (DepthPrepassMode)reader.ReadByte(),
                ShadowDistance = reader.ReadSingle(),
                PostProcessTier = (PostProcessTier)reader.ReadByte()
            };
        }
    }
}
