using helengine.editor;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Rewrites committed rendering editor scenes from legacy positional component payloads into the tagged editor payload format.
    /// </summary>
    public sealed class ExistingRenderingSceneTaggedMigrator {
        /// <summary>
        /// Migrates every committed rendering scene beneath the supplied project root to the tagged editor payload format.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the committed `assets/Scenes/rendering` folder.</param>
        public void MigrateAll(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string renderingSceneRootPath = Path.Combine(projectRootPath, "assets", "Scenes", "rendering");
            if (!Directory.Exists(renderingSceneRootPath)) {
                throw new InvalidOperationException($"Rendering scene directory was not found: {renderingSceneRootPath}");
            }

            string[] scenePaths = Directory.GetFiles(renderingSceneRootPath, "*.helen", SearchOption.TopDirectoryOnly);
            for (int sceneIndex = 0; sceneIndex < scenePaths.Length; sceneIndex++) {
                RewriteScene(scenePaths[sceneIndex]);
            }
        }

        /// <summary>
        /// Rewrites one committed rendering scene file in place.
        /// </summary>
        /// <param name="scenePath">Absolute scene file path to rewrite.</param>
        void RewriteScene(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            }

            SceneAsset sceneAsset;
            using (FileStream inputStream = File.OpenRead(scenePath)) {
                sceneAsset = (SceneAsset)EditorAssetBinarySerializer.Deserialize(inputStream);
            }

            SceneEntityAsset[] rewrittenRoots = RewriteEntities(sceneAsset.RootEntities);
            SceneAsset rewrittenSceneAsset = new SceneAsset {
                Id = sceneAsset.Id,
                AssetReferences = sceneAsset.AssetReferences,
                RootEntities = rewrittenRoots
            };

            using FileStream outputStream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(outputStream, rewrittenSceneAsset);
        }

        /// <summary>
        /// Rewrites one serialized entity hierarchy into the tagged editor payload format.
        /// </summary>
        /// <param name="entities">Serialized entities that should be rewritten.</param>
        /// <returns>Copied serialized entities with rewritten component payloads.</returns>
        SceneEntityAsset[] RewriteEntities(SceneEntityAsset[] entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            SceneEntityAsset[] rewrittenEntities = new SceneEntityAsset[entities.Length];
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++) {
                SceneEntityAsset entity = entities[entityIndex];
                rewrittenEntities[entityIndex] = new SceneEntityAsset {
                    Id = entity.Id,
                    Name = entity.Name,
                    LocalPosition = entity.LocalPosition,
                    LocalScale = entity.LocalScale,
                    LocalOrientation = entity.LocalOrientation,
                    Components = RewriteComponents(entity.Components),
                    Children = RewriteEntities(entity.Children)
                };
            }

            return rewrittenEntities;
        }

        /// <summary>
        /// Rewrites one serialized component list into the tagged editor payload format.
        /// </summary>
        /// <param name="components">Serialized components that should be rewritten.</param>
        /// <returns>Copied serialized components with rewritten payloads where needed.</returns>
        SceneComponentAssetRecord[] RewriteComponents(SceneComponentAssetRecord[] components) {
            if (components == null) {
                throw new ArgumentNullException(nameof(components));
            }

            SceneComponentAssetRecord[] rewrittenComponents = new SceneComponentAssetRecord[components.Length];
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++) {
                rewrittenComponents[componentIndex] = RewriteComponent(components[componentIndex]);
            }

            return rewrittenComponents;
        }

        /// <summary>
        /// Rewrites one serialized component record when it still uses a legacy positional payload.
        /// </summary>
        /// <param name="record">Serialized component record to inspect.</param>
        /// <returns>Original or rewritten component record.</returns>
        SceneComponentAssetRecord RewriteComponent(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.Equals(record.ComponentTypeId, "helengine.CameraComponent", StringComparison.Ordinal)) {
                return RewriteCameraRecord(record);
            }
            if (string.Equals(record.ComponentTypeId, "helengine.MeshComponent", StringComparison.Ordinal)) {
                return RewriteMeshRecord(record);
            }
            if (string.Equals(record.ComponentTypeId, "helengine.PointLightComponent", StringComparison.Ordinal)) {
                return RewritePointLightRecord(record);
            }
            if (string.Equals(record.ComponentTypeId, "helengine.SpotLightComponent", StringComparison.Ordinal)) {
                return RewriteSpotLightRecord(record);
            }
            if (string.Equals(record.ComponentTypeId, "helengine.DirectionalLightComponent", StringComparison.Ordinal)) {
                return RewriteDirectionalLightRecord(record);
            }

            return record;
        }

        /// <summary>
        /// Rewrites one camera record when it still uses the legacy positional payload shape.
        /// </summary>
        /// <param name="record">Serialized camera record to inspect.</param>
        /// <returns>Original or rewritten camera record.</returns>
        SceneComponentAssetRecord RewriteCameraRecord(SceneComponentAssetRecord record) {
            if (IsTaggedPayload(record.Payload)) {
                return record;
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1 && version != 2) {
                throw new InvalidOperationException($"Unsupported legacy camera payload version '{version}'.");
            }

            byte cameraDrawOrder = reader.ReadByte();
            ushort layerMask = reader.ReadUInt16();
            float4 viewport = reader.ReadFloat4();
            CameraClearSettings clearSettings = new CameraClearSettings(
                reader.ReadByte() != 0,
                reader.ReadFloat4(),
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadByte() != 0,
                reader.ReadByte());
            CameraRenderSettings renderSettings = version >= 2
                ? new CameraRenderSettings {
                    DepthPrepassMode = (DepthPrepassMode)reader.ReadByte(),
                    ShadowDistance = reader.ReadSingle(),
                    PostProcessTier = (PostProcessTier)reader.ReadByte()
                }
                : new CameraRenderSettings();

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(cameraDrawOrder));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(layerMask));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(viewport));
            writer.WriteField("ClearSettings", fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(fieldWriter, clearSettings));
            writer.WriteField("RenderSettings", fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(fieldWriter, renderSettings));
            return CreateRewrittenRecord(record, writer.BuildPayload());
        }

        /// <summary>
        /// Rewrites one mesh record when it still uses the legacy positional payload shape.
        /// </summary>
        /// <param name="record">Serialized mesh record to inspect.</param>
        /// <returns>Original or rewritten mesh record.</returns>
        SceneComponentAssetRecord RewriteMeshRecord(SceneComponentAssetRecord record) {
            if (IsTaggedPayload(record.Payload)) {
                return record;
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != 1) {
                throw new InvalidOperationException($"Unsupported legacy mesh payload version '{version}'.");
            }

            SceneAssetReference modelReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
            SceneAssetReference materialReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
            byte renderOrder3D = reader.ReadByte();

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("ModelReference", fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, modelReference));
            writer.WriteField("MaterialReference", fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, materialReference));
            writer.WriteField("RenderOrder3D", fieldWriter => fieldWriter.WriteByte(renderOrder3D));
            return CreateRewrittenRecord(record, writer.BuildPayload());
        }

        /// <summary>
        /// Rewrites one point-light record when it still uses the legacy strict light payload shape.
        /// </summary>
        /// <param name="record">Serialized point-light record to inspect.</param>
        /// <returns>Original or rewritten point-light record.</returns>
        SceneComponentAssetRecord RewritePointLightRecord(SceneComponentAssetRecord record) {
            if (IsTaggedPayload(record.Payload)) {
                return record;
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported legacy point-light payload version '{version}'.");
            }

            PointLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadPointLight(reader);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField("Range", fieldWriter => fieldWriter.WriteSingle(lightComponent.Range));
            return CreateRewrittenRecord(record, writer.BuildPayload());
        }

        /// <summary>
        /// Rewrites one spot-light record when it still uses the legacy strict light payload shape.
        /// </summary>
        /// <param name="record">Serialized spot-light record to inspect.</param>
        /// <returns>Original or rewritten spot-light record.</returns>
        SceneComponentAssetRecord RewriteSpotLightRecord(SceneComponentAssetRecord record) {
            if (IsTaggedPayload(record.Payload)) {
                return record;
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported legacy spot-light payload version '{version}'.");
            }

            SpotLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadSpotLight(reader);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField("Range", fieldWriter => fieldWriter.WriteSingle(lightComponent.Range));
            writer.WriteField("InnerConeAngleDegrees", fieldWriter => fieldWriter.WriteSingle(lightComponent.InnerConeAngleDegrees));
            writer.WriteField("OuterConeAngleDegrees", fieldWriter => fieldWriter.WriteSingle(lightComponent.OuterConeAngleDegrees));
            return CreateRewrittenRecord(record, writer.BuildPayload());
        }

        /// <summary>
        /// Rewrites one directional-light record when it still uses the legacy strict light payload shape.
        /// </summary>
        /// <param name="record">Serialized directional-light record to inspect.</param>
        /// <returns>Original or rewritten directional-light record.</returns>
        SceneComponentAssetRecord RewriteDirectionalLightRecord(SceneComponentAssetRecord record) {
            if (IsTaggedPayload(record.Payload)) {
                return record;
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported legacy directional-light payload version '{version}'.");
            }

            DirectionalLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadDirectionalLight(reader);
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            LightComponentTaggedFieldEncoding.WriteCommonFields(writer, lightComponent);
            writer.WriteField("ShadowDistance", fieldWriter => fieldWriter.WriteSingle(lightComponent.ShadowDistance));
            return CreateRewrittenRecord(record, writer.BuildPayload());
        }

        /// <summary>
        /// Gets whether one payload already conforms to the tagged editor payload format.
        /// </summary>
        /// <param name="payload">Serialized payload to inspect.</param>
        /// <returns>True when the payload is already tagged; otherwise false.</returns>
        bool IsTaggedPayload(byte[] payload) {
            if (payload == null || payload.Length == 0) {
                return false;
            }

            try {
                _ = new EditorTaggedSceneComponentFieldReader(payload);
                return true;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Creates one copied component record with a rewritten payload.
        /// </summary>
        /// <param name="record">Original component record being rewritten.</param>
        /// <param name="payload">Rewritten payload bytes.</param>
        /// <returns>Copied component record with the supplied payload.</returns>
        static SceneComponentAssetRecord CreateRewrittenRecord(SceneComponentAssetRecord record, byte[] payload) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = payload
            };
        }
    }
}
