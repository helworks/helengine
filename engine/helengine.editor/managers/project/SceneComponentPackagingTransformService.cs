using helengine.baseplatform.Builders;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor {
    /// <summary>
    /// Rewrites shared scene component payloads into packaged runtime forms.
    /// </summary>
    public sealed class SceneComponentPackagingTransformService {
        /// <summary>
        /// Current payload version for serialized mesh component scene records.
        /// </summary>
        const byte MeshComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized camera component scene records.
        /// </summary>
        const byte CameraComponentPayloadVersion = 2;

        /// <summary>
        /// Current payload version for serialized FPS component scene records.
        /// </summary>
        const byte FPSComponentPayloadVersion = 2;

        /// <summary>
        /// Current payload version for serialized text component scene records.
        /// </summary>
        const byte TextComponentPayloadVersion = 1;

        /// <summary>
        /// Stable serialized component id for mesh components.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";

        /// <summary>
        /// Stable serialized component id for camera components.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";

        /// <summary>
        /// Stable serialized component id for FPS overlay components.
        /// </summary>
        const string FPSComponentTypeId = "helengine.FPSComponent";

        /// <summary>
        /// Stable serialized component id for text components.
        /// </summary>
        const string TextComponentTypeId = "helengine.TextComponent";

        /// <summary>
        /// Stable serialized component id for rounded rectangle components.
        /// </summary>
        const string RoundedRectComponentTypeId = "helengine.RoundedRectComponent";

        /// <summary>
        /// Stable serialized component id for directional light components.
        /// </summary>
        const string DirectionalLightComponentTypeId = "helengine.DirectionalLightComponent";

        /// <summary>
        /// Stable serialized component id for point light components.
        /// </summary>
        const string PointLightComponentTypeId = "helengine.PointLightComponent";

        /// <summary>
        /// Stable serialized component id for spot light components.
        /// </summary>
        const string SpotLightComponentTypeId = "helengine.SpotLightComponent";

        /// <summary>
        /// Generated provider id reserved for the editor's built-in font asset.
        /// </summary>
        const string EditorGeneratedProviderId = "editor";

        /// <summary>
        /// Stable asset id used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontAssetId = "ui-font";

        /// <summary>
        /// Relative packaged font path used by the editor's built-in font asset.
        /// </summary>
        const string EditorFontRelativePath = "cooked/fonts/default.hefont";

        /// <summary>
        /// Runtime scene layer used by the current Windows player loader for materialized entities.
        /// </summary>
        const ushort RuntimeSceneLayerMask = 0b00000001;

        /// <summary>
        /// Stable generated-asset provider id used by engine-generated scene references.
        /// </summary>
        const string EngineGeneratedProviderId = "engine";

        /// <summary>
        /// Stable generated model asset id for the built-in cube primitive.
        /// </summary>
        const string CubeGeneratedAssetId = "engine:model:cube";

        /// <summary>
        /// Stable generated model asset id for the built-in plane primitive.
        /// </summary>
        const string PlaneGeneratedAssetId = "engine:model:plane";

        /// <summary>
        /// Stable generated material asset id for the built-in standard material.
        /// </summary>
        const string StandardGeneratedMaterialAssetId = "engine:material:standard";

        /// <summary>
        /// Shader source file used by the packaged generated standard material.
        /// </summary>
        const string StandardShaderFileName = "ForwardStandardShader.hlsl";

        /// <summary>
        /// Vertex program name used by the packaged generated standard material.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";

        /// <summary>
        /// Pixel program name used by the packaged generated standard material.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";

        /// <summary>
        /// Shader variant name used by the packaged generated standard material.
        /// </summary>
        const string StandardShaderVariantName = "default";

        /// <summary>
        /// Relative packaged material path used by generated primitive scenes.
        /// </summary>
        const string StandardGeneratedMaterialRelativePath = "cooked/engine/materials/standard.hasset";

        /// <summary>
        /// Relative packaged shader path used by generated primitive scenes.
        /// </summary>
        const string StandardGeneratedShaderRelativePath = "cooked/shaders/ForwardStandardShader.dx11.hasset";

        /// <summary>
        /// Absolute source assets root used to resolve project-relative scene ids and file-backed asset references.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Content manager used to load serialized scene and material assets from the project.
        /// </summary>
        readonly ContentManager ProjectContentManager;

        /// <summary>
        /// Asset import manager used to resolve file-backed source models into processed `ModelAsset` payloads.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Resolver used to obtain processed `ModelAsset` payloads for file-backed source models.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;

        /// <summary>
        /// Deduplicated shader asset ids referenced while packaging the current scene set.
        /// </summary>
        readonly List<string> ReferencedShaderAssetIds;

        /// <summary>
        /// Fast lookup used to deduplicate referenced shader asset ids while preserving discovery order.
        /// </summary>
        readonly HashSet<string> ReferencedShaderAssetIdsSet;

        /// <summary>
        /// Service used to load per-platform material settings sidecars for packaged material assets.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialAssetSettingsService;

        /// <summary>
        /// Target platform id whose material settings should drive packaged compatibility payloads.
        /// </summary>
        readonly string TargetPlatformId;

        /// <summary>
        /// Builder used to translate schema-driven material settings into cooked runtime material bytes.
        /// </summary>
        readonly IPlatformAssetBuilder MaterialBuilder;

        /// <summary>
        /// Selected build profile id for the current packaging operation.
        /// </summary>
        readonly string SelectedBuildProfileId;

        /// <summary>
        /// Selected graphics profile id for the current packaging operation.
        /// </summary>
        readonly string SelectedGraphicsProfileId;

        /// <summary>
        /// Reflected scripted-component schema builder used for automatic scripted payload rewrites.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder ScriptComponentSchemaBuilder;

        /// <summary>
        /// Automatic scripted-component descriptor used to interpret editor tagged payloads before packaging.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticScriptComponentDescriptor;
        /// <summary>
        /// Camera descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly CameraComponentPersistenceDescriptor CameraComponentDescriptor;
        /// <summary>
        /// Rounded-rectangle descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly RoundedRectComponentPersistenceDescriptor RoundedRectComponentDescriptor;
        /// <summary>
        /// Demo menu root descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly MenuComponentPersistenceDescriptor DemoMenuBuildComponentDescriptor;
        /// <summary>
        /// Demo menu panel descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly MenuPanelComponentPersistenceDescriptor DemoMenuPanelComponentDescriptor;
        /// <summary>
        /// Demo menu item descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly MenuItemComponentPersistenceDescriptor DemoMenuItemComponentDescriptor;
        /// <summary>
        /// Demo menu selected-description descriptor used to interpret tagged editor payloads before rewriting packaged runtime bytes.
        /// </summary>
        readonly MenuSelectedDescriptionComponentPersistenceDescriptor DemoMenuSelectedDescriptionComponentDescriptor;

        /// <summary>
        /// Initializes one shared scene-component transform service.
        /// </summary>
        /// <param name="assetsRootPath">Absolute source assets root path.</param>
        /// <param name="projectContentManager">Project content manager used to load serialized assets.</param>
        /// <param name="assetImportManager">Asset import manager used for file-backed model assets.</param>
        /// <param name="fileSystemModelResolver">Resolver used to obtain processed model assets for file-backed source references.</param>
        /// <param name="referencedShaderAssetIds">Deduplicated shader ids collected during packaging.</param>
        /// <param name="referencedShaderAssetIdsSet">Fast lookup set used to deduplicate shader ids.</param>
        /// <param name="targetPlatformId">Target platform id whose material settings should be used during packaging.</param>
        /// <param name="materialBuilder">Optional builder used to translate schema-driven material settings.</param>
        /// <param name="selectedBuildProfileId">Selected build profile id for the current packaging operation.</param>
        /// <param name="selectedGraphicsProfileId">Selected graphics profile id for the current packaging operation.</param>
        public SceneComponentPackagingTransformService(
            string assetsRootPath,
            ContentManager projectContentManager,
            AssetImportManager assetImportManager,
            EditorFileSystemModelResolver fileSystemModelResolver,
            List<string> referencedShaderAssetIds,
            HashSet<string> referencedShaderAssetIdsSet,
            string targetPlatformId = "",
            IPlatformAssetBuilder materialBuilder = null,
            string selectedBuildProfileId = "",
            string selectedGraphicsProfileId = "") {
            AssetsRootPath = string.IsNullOrWhiteSpace(assetsRootPath)
                ? throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath))
                : Path.GetFullPath(assetsRootPath);
            ProjectContentManager = projectContentManager ?? throw new ArgumentNullException(nameof(projectContentManager));
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            FileSystemModelResolver = fileSystemModelResolver ?? throw new ArgumentNullException(nameof(fileSystemModelResolver));
            ReferencedShaderAssetIds = referencedShaderAssetIds ?? throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            ReferencedShaderAssetIdsSet = referencedShaderAssetIdsSet ?? throw new ArgumentNullException(nameof(referencedShaderAssetIdsSet));
            MaterialAssetSettingsService = new MaterialAssetSettingsService();
            TargetPlatformId = string.IsNullOrWhiteSpace(targetPlatformId) ? "windows" : targetPlatformId;
            MaterialBuilder = materialBuilder;
            SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
            SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
            ScriptComponentSchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            AutomaticScriptComponentDescriptor = new AutomaticScriptComponentPersistenceDescriptor(ScriptComponentSchemaBuilder);
            CameraComponentDescriptor = new CameraComponentPersistenceDescriptor();
            RoundedRectComponentDescriptor = new RoundedRectComponentPersistenceDescriptor();
            DemoMenuBuildComponentDescriptor = new MenuComponentPersistenceDescriptor();
            DemoMenuPanelComponentDescriptor = new MenuPanelComponentPersistenceDescriptor();
            DemoMenuItemComponentDescriptor = new MenuItemComponentPersistenceDescriptor();
            DemoMenuSelectedDescriptionComponentDescriptor = new MenuSelectedDescriptionComponentPersistenceDescriptor();
        }

        /// <summary>
        /// Attempts to rewrite one shared component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Component record to transform.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <param name="transformedRecord">Rewritten component record when successful.</param>
        /// <returns>True when a transformation was applied; otherwise false.</returns>
        public bool TryTransform(SceneComponentAssetRecord record, string buildRootPath, out SceneComponentAssetRecord transformedRecord) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (string.Equals(record.ComponentTypeId, MeshComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteMeshComponentRecord(record, buildRootPath);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, CameraComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteCameraComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, FPSComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteFPSComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, TextComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteTextComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, RoundedRectComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteRoundedRectComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, DirectionalLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDirectionalLightComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, PointLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewritePointLightComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, SpotLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteSpotLightComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, MenuComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDemoMenuBuildComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, MenuPanelComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDemoMenuPanelComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, MenuItemComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDemoMenuItemComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, MenuSelectedDescriptionComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDemoMenuSelectedDescriptionComponentRecord(record);
                return true;
            }

            if (IsAutomaticScriptComponentTypeId(record.ComponentTypeId)) {
                transformedRecord = RewriteAutomaticScriptComponentRecord(record);
                return true;
            }

            transformedRecord = null;
            return false;
        }

        /// <summary>
        /// Returns whether one serialized component type id identifies an eligible scripted component that should be rewritten into packaged ordinal form.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <returns>True when the component type id identifies an eligible scripted component.</returns>
        bool IsAutomaticScriptComponentTypeId(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return false;
            }

            Type componentType = Type.GetType(componentTypeId, false);
            if (componentType == null) {
                return false;
            }
            if (!typeof(Component).IsAssignableFrom(componentType)) {
                return false;
            }

            return componentType.Assembly != typeof(Component).Assembly;
        }

        /// <summary>
        /// Rewrites one named editor scripted-component payload into the strict packaged ordinal payload shape.
        /// </summary>
        /// <param name="record">Serialized scripted-component record to rewrite.</param>
        /// <returns>Rewritten scripted-component record.</returns>
        SceneComponentAssetRecord RewriteAutomaticScriptComponentRecord(SceneComponentAssetRecord record) {
            Component component = AutomaticScriptComponentDescriptor.DeserializeComponent(record, null, null);
            ScriptComponentReflectionSchema schema = ScriptComponentSchemaBuilder.Build(component.GetType());

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedValue(writer, member.ValueType, member.GetValue(component));
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized mesh payload into the strict runtime mesh payload shape.
        /// </summary>
        /// <param name="record">Serialized mesh component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten mesh component record.</returns>
        SceneComponentAssetRecord RewriteMeshComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != MeshComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported mesh component payload version '{version}'.");
            }

            SceneAssetReference modelReference = RewriteModelReference(ReadOptionalReference(reader), buildRootPath);
            SceneAssetReference materialReference = RewriteMaterialReference(ReadOptionalReference(reader), buildRootPath);
            byte renderOrder3D = reader.ReadByte();

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MeshComponentPayloadVersion);
            WriteOptionalReference(writer, modelReference);
            WriteOptionalReference(writer, materialReference);
            writer.WriteByte(renderOrder3D);

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized camera payload into the strict runtime camera payload shape.
        /// </summary>
        /// <param name="record">Serialized camera component record to rewrite.</param>
        /// <returns>Rewritten camera component record.</returns>
        SceneComponentAssetRecord RewriteCameraComponentRecord(SceneComponentAssetRecord record) {
            CameraComponent component = AssertCameraComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CameraComponentPayloadVersion);
            writer.WriteByte(component.CameraDrawOrder);
            writer.WriteUInt16(NormalizePackagedCameraLayerMask(component.LayerMask));
            WriteFloat4(writer, component.Viewport);
            WriteClearSettings(writer, component.ClearSettings);
            WriteRenderSettings(writer, component.RenderSettings ?? new CameraRenderSettings());

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized FPS payload into the strict runtime FPS payload shape.
        /// </summary>
        /// <param name="record">Serialized FPS component record to rewrite.</param>
        /// <returns>Rewritten FPS component record.</returns>
        SceneComponentAssetRecord RewriteFPSComponentRecord(SceneComponentAssetRecord record) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != FPSComponentPayloadVersion && version != 1) {
                throw new InvalidOperationException($"Unsupported FPS component payload version '{version}'.");
            }

            SceneAssetReference fontReference = version >= 2
                ? FontAssetScenePersistenceSupport.ReadOptionalReference(reader)
                : FontAssetScenePersistenceSupport.BuildEditorFontReference();
            double refreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64());
            int2 padding = reader.ReadInt2();
            byte renderOrder2D = reader.ReadByte();

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(FPSComponentPayloadVersion);
            WriteOptionalReference(writer, RewriteFontReference(fontReference));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(refreshIntervalSeconds));
            writer.WriteInt2(padding);
            writer.WriteByte(renderOrder2D);

            return new SceneComponentAssetRecord {
                ComponentTypeId = FPSComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized text payload into the strict runtime text payload shape.
        /// </summary>
        /// <param name="record">Serialized text component record to rewrite.</param>
        /// <returns>Rewritten text component record.</returns>
        SceneComponentAssetRecord RewriteTextComponentRecord(SceneComponentAssetRecord record) {
            ReadTaggedTextComponentRecord(
                record,
                out SceneAssetReference fontReference,
                out string text,
                out bool wrapText,
                out int2 size,
                out byte4 color,
                out float4 sourceRect,
                out float rotation,
                out byte renderOrder2D,
                out byte layerMask,
                out bool selectionEnabled);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(TextComponentPayloadVersion);
            WriteOptionalReference(writer, RewriteFontReference(fontReference));
            writer.WriteString(text);
            writer.WriteByte(wrapText ? (byte)1 : (byte)0);
            writer.WriteInt2(size);
            FontAssetScenePersistenceSupport.WriteByte4(writer, color);
            WriteFloat4(writer, sourceRect);
            writer.WriteSingle(rotation);
            writer.WriteByte(renderOrder2D);
            writer.WriteByte(layerMask);
            writer.WriteByte(selectionEnabled ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = TextComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized rounded-rectangle payload into the strict runtime payload shape.
        /// </summary>
        /// <param name="record">Serialized rounded rectangle component record to rewrite.</param>
        /// <returns>Rewritten rounded rectangle component record.</returns>
        SceneComponentAssetRecord RewriteRoundedRectComponentRecord(SceneComponentAssetRecord record) {
            RoundedRectComponent component = AssertRoundedRectComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(component.RenderOrder2D);
            writer.WriteByte(component.LayerMask);
            writer.WriteInt32((int)component.Corners);
            writer.WriteSingle(component.Rotation);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.Color);
            writer.WriteFloat4(component.SourceRect);
            writer.WriteInt2(component.Size);
            writer.WriteSingle(component.Radius);
            writer.WriteSingle(component.BorderThickness);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.FillColor);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.BorderColor);

            return new SceneComponentAssetRecord {
                ComponentTypeId = RoundedRectComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized directional-light payload into the strict runtime light payload shape.
        /// </summary>
        /// <param name="record">Serialized directional light component record to rewrite.</param>
        /// <returns>Rewritten directional light component record.</returns>
        SceneComponentAssetRecord RewriteDirectionalLightComponentRecord(SceneComponentAssetRecord record) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported directional light payload version '{version}'.");
            }

            DirectionalLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadDirectionalLight(reader);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WriteDirectionalLight(writer, lightComponent);

            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalLightComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized point-light payload into the strict runtime light payload shape.
        /// </summary>
        /// <param name="record">Serialized point light component record to rewrite.</param>
        /// <returns>Rewritten point light component record.</returns>
        SceneComponentAssetRecord RewritePointLightComponentRecord(SceneComponentAssetRecord record) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported point light payload version '{version}'.");
            }

            PointLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadPointLight(reader);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WritePointLight(writer, lightComponent);

            return new SceneComponentAssetRecord {
                ComponentTypeId = PointLightComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized spot-light payload into the strict runtime light payload shape.
        /// </summary>
        /// <param name="record">Serialized spot light component record to rewrite.</param>
        /// <returns>Rewritten spot light component record.</returns>
        SceneComponentAssetRecord RewriteSpotLightComponentRecord(SceneComponentAssetRecord record) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != LightComponentScenePayloadSerializer.CurrentVersion) {
                throw new InvalidOperationException($"Unsupported spot light payload version '{version}'.");
            }

            SpotLightComponent lightComponent = LightComponentScenePayloadSerializer.ReadSpotLight(reader);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(LightComponentScenePayloadSerializer.CurrentVersion);
            LightComponentScenePayloadSerializer.WriteSpotLight(writer, lightComponent);

            return new SceneComponentAssetRecord {
                ComponentTypeId = SpotLightComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized demo-menu root payload into the strict runtime menu payload shape.
        /// </summary>
        /// <param name="record">Serialized demo-menu root component record to rewrite.</param>
        /// <returns>Rewritten demo-menu root component record.</returns>
        SceneComponentAssetRecord RewriteDemoMenuBuildComponentRecord(SceneComponentAssetRecord record) {
            MenuComponent component = AssertDemoMenuBuildComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuComponent.CurrentVersion);
            writer.WriteString(component.ProviderTypeName);
            writer.WriteString(component.InitialPanelId);

            return new SceneComponentAssetRecord {
                ComponentTypeId = MenuComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized demo-menu panel payload into the strict runtime menu payload shape.
        /// </summary>
        /// <param name="record">Serialized demo-menu panel component record to rewrite.</param>
        /// <returns>Rewritten demo-menu panel component record.</returns>
        SceneComponentAssetRecord RewriteDemoMenuPanelComponentRecord(SceneComponentAssetRecord record) {
            MenuPanelComponent component = AssertDemoMenuPanelComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuPanelComponent.CurrentVersion);
            writer.WriteString(component.PanelId);

            return new SceneComponentAssetRecord {
                ComponentTypeId = MenuPanelComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized demo-menu item payload into the strict runtime menu payload shape.
        /// </summary>
        /// <param name="record">Serialized demo-menu item component record to rewrite.</param>
        /// <returns>Rewritten demo-menu item component record.</returns>
        SceneComponentAssetRecord RewriteDemoMenuItemComponentRecord(SceneComponentAssetRecord record) {
            MenuItemComponent component = AssertDemoMenuItemComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuItemComponent.CurrentVersion);
            writer.WriteString(component.PanelId);
            writer.WriteString(component.ItemId);
            writer.WriteString(component.Description);
            writer.WriteByte((byte)component.ActionKind);
            writer.WriteString(component.TargetId);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.IdleFillColor);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.IdleBorderColor);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.SelectedFillColor);
            FontAssetScenePersistenceSupport.WriteByte4(writer, component.SelectedBorderColor);

            return new SceneComponentAssetRecord {
                ComponentTypeId = MenuItemComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized selected-description marker payload into the strict runtime marker payload shape.
        /// </summary>
        /// <param name="record">Serialized selected-description marker component record to rewrite.</param>
        /// <returns>Rewritten selected-description marker component record.</returns>
        SceneComponentAssetRecord RewriteDemoMenuSelectedDescriptionComponentRecord(SceneComponentAssetRecord record) {
            AssertDemoMenuSelectedDescriptionComponent(record);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuSelectedDescriptionComponent.CurrentVersion);

            return new SceneComponentAssetRecord {
                ComponentTypeId = MenuSelectedDescriptionComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one tagged camera payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <returns>Deserialized camera component.</returns>
        CameraComponent AssertCameraComponent(SceneComponentAssetRecord record) {
            Component component = CameraComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not CameraComponent cameraComponent) {
                throw new InvalidOperationException("Camera component payload did not materialize correctly before packaging.");
            }

            return cameraComponent;
        }

        /// <summary>
        /// Reads one tagged text payload into the runtime values needed for packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <param name="fontReference">Resolved serialized font reference associated with the text component.</param>
        /// <param name="text">Persisted text content.</param>
        /// <param name="wrapText">Persisted wrap mode.</param>
        /// <param name="size">Persisted layout size.</param>
        /// <param name="color">Persisted glyph color.</param>
        /// <param name="sourceRect">Persisted source rectangle.</param>
        /// <param name="rotation">Persisted rotation.</param>
        /// <param name="renderOrder2D">Persisted render order.</param>
        /// <param name="layerMask">Persisted layer mask.</param>
        /// <param name="selectionEnabled">Persisted selection flag.</param>
        void ReadTaggedTextComponentRecord(
            SceneComponentAssetRecord record,
            out SceneAssetReference fontReference,
            out string text,
            out bool wrapText,
            out int2 size,
            out byte4 color,
            out float4 sourceRect,
            out float rotation,
            out byte renderOrder2D,
            out byte layerMask,
            out bool selectionEnabled) {
            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
            fontReference = null;
            text = string.Empty;
            wrapText = false;
            size = int2.Zero;
            color = new byte4(255, 255, 255, 255);
            sourceRect = new float4(0f, 0f, 1f, 1f);
            rotation = 0f;
            renderOrder2D = 0;
            layerMask = 0;
            selectionEnabled = false;

            if (reader.TryGetFieldReader("FontReference", out EngineBinaryReader fontReferenceReader)) {
                using (fontReferenceReader) {
                    fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
                }
            }
            if (reader.TryGetFieldReader("Text", out EngineBinaryReader textReader)) {
                using (textReader) {
                    text = textReader.ReadString();
                }
            }
            if (reader.TryGetFieldReader("WrapText", out EngineBinaryReader wrapTextReader)) {
                using (wrapTextReader) {
                    wrapText = wrapTextReader.ReadByte() != 0;
                }
            }
            if (reader.TryGetFieldReader("Size", out EngineBinaryReader sizeReader)) {
                using (sizeReader) {
                    size = sizeReader.ReadInt2();
                }
            }
            if (reader.TryGetFieldReader("Color", out EngineBinaryReader colorReader)) {
                using (colorReader) {
                    color = SceneComponentBinaryFieldEncoding.ReadByte4(colorReader);
                }
            }
            if (reader.TryGetFieldReader("SourceRect", out EngineBinaryReader sourceRectReader)) {
                using (sourceRectReader) {
                    sourceRect = sourceRectReader.ReadFloat4();
                }
            }
            if (reader.TryGetFieldReader("Rotation", out EngineBinaryReader rotationReader)) {
                using (rotationReader) {
                    rotation = rotationReader.ReadSingle();
                }
            }
            if (reader.TryGetFieldReader("RenderOrder2D", out EngineBinaryReader renderOrderReader)) {
                using (renderOrderReader) {
                    renderOrder2D = renderOrderReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader("LayerMask", out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    layerMask = layerMaskReader.ReadByte();
                }
            }
            if (reader.TryGetFieldReader("SelectionEnabled", out EngineBinaryReader selectionEnabledReader)) {
                using (selectionEnabledReader) {
                    selectionEnabled = selectionEnabledReader.ReadByte() != 0;
                }
            }

            if (fontReference == null) {
                throw new InvalidOperationException("Text component payload did not provide a font reference before packaging.");
            }
        }

        /// <summary>
        /// Deserializes one tagged rounded-rectangle payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <returns>Deserialized rounded rectangle component.</returns>
        RoundedRectComponent AssertRoundedRectComponent(SceneComponentAssetRecord record) {
            Component component = RoundedRectComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not RoundedRectComponent roundedRectComponent) {
                throw new InvalidOperationException("Rounded rectangle component payload did not materialize correctly before packaging.");
            }

            return roundedRectComponent;
        }

        /// <summary>
        /// Deserializes one tagged demo menu root payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <returns>Deserialized demo menu root component.</returns>
        MenuComponent AssertDemoMenuBuildComponent(SceneComponentAssetRecord record) {
            Component component = DemoMenuBuildComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not MenuComponent demoMenuBuildComponent) {
                throw new InvalidOperationException("Demo menu build component payload did not materialize correctly before packaging.");
            }

            return demoMenuBuildComponent;
        }

        /// <summary>
        /// Deserializes one tagged demo menu panel payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <returns>Deserialized demo menu panel component.</returns>
        MenuPanelComponent AssertDemoMenuPanelComponent(SceneComponentAssetRecord record) {
            Component component = DemoMenuPanelComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not MenuPanelComponent demoMenuPanelComponent) {
                throw new InvalidOperationException("Demo menu panel component payload did not materialize correctly before packaging.");
            }

            return demoMenuPanelComponent;
        }

        /// <summary>
        /// Deserializes one tagged demo menu item payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <returns>Deserialized demo menu item component.</returns>
        MenuItemComponent AssertDemoMenuItemComponent(SceneComponentAssetRecord record) {
            Component component = DemoMenuItemComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not MenuItemComponent demoMenuItemComponent) {
                throw new InvalidOperationException("Demo menu item component payload did not materialize correctly before packaging.");
            }

            return demoMenuItemComponent;
        }

        /// <summary>
        /// Deserializes one tagged demo menu selected-description payload into its live component shape before packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        void AssertDemoMenuSelectedDescriptionComponent(SceneComponentAssetRecord record) {
            Component component = DemoMenuSelectedDescriptionComponentDescriptor.DeserializeComponent(record, null, null);
            if (component is not MenuSelectedDescriptionComponent) {
                throw new InvalidOperationException("Demo menu selected-description component payload did not materialize correctly before packaging.");
            }
        }

        SceneAssetReference RewriteFontReference(SceneAssetReference reference) {
            if (reference == null) {
                throw new InvalidOperationException("FPSComponent requires a font reference before packaging.");
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                if (!string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}'.");
                }
                if (!string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}'.");
                }

                return CreateFontFileReference(EditorFontRelativePath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                string relativePath = NormalizeRelativePath(reference.RelativePath);
                return CreateFontFileReference(relativePath);
            }

            throw new InvalidOperationException($"Unsupported font reference source kind '{reference.SourceKind}'.");
        }

        /// <summary>
        /// Builds the stable generated reference used for the editor's built-in font asset.
        /// </summary>
        /// <returns>Generated editor-font scene reference.</returns>
        SceneAssetReference BuildEditorFontReference() {
            return FontAssetScenePersistenceSupport.BuildEditorFontReference();
        }

        SceneAssetReference CreateFontFileReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = NormalizeRelativePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        SceneAssetReference RewriteModelReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                return null;
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return RewriteGeneratedModelReference(reference, buildRootPath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return RewriteFileSystemModelReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Unsupported model reference source kind '{reference.SourceKind}'.");
        }

        SceneAssetReference RewriteMaterialReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                return null;
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return RewriteGeneratedMaterialReference(reference, buildRootPath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return RewriteFileSystemMaterialReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Unsupported material reference source kind '{reference.SourceKind}'.");
        }

        SceneAssetReference RewriteGeneratedModelReference(SceneAssetReference reference, string buildRootPath) {
            if (!string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated model provider '{reference.ProviderId}'.");
            }

            if (string.Equals(reference.AssetId, CubeGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "cooked/engine/models/cube.hasset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GenerateCubeMesh(float3.Zero, float3.One));
                return CreateFileSystemReference(relativePath);
            }

            if (string.Equals(reference.AssetId, PlaneGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "cooked/engine/models/plane.hasset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One));
                return CreateFileSystemReference(relativePath);
            }

            throw new InvalidOperationException($"Unsupported generated model asset id '{reference.AssetId}'.");
        }

        SceneAssetReference RewriteFileSystemModelReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            ModelAsset modelAsset = FileSystemModelResolver.ResolveModelAsset(sourcePath);
            string relativePath = BuildImportedModelRelativePath(reference.RelativePath);
            WriteAsset(Path.Combine(buildRootPath, relativePath), modelAsset);
            return CreateFileSystemReference(relativePath);
        }

        SceneAssetReference RewriteGeneratedMaterialReference(SceneAssetReference reference, string buildRootPath) {
            if (!string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material provider '{reference.ProviderId}'.");
            }
            if (!string.Equals(reference.AssetId, StandardGeneratedMaterialAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material asset id '{reference.AssetId}'.");
            }

            EnsureGeneratedStandardMaterialAssets(buildRootPath);
            return CreateFileSystemReference(StandardGeneratedMaterialRelativePath);
        }

        SceneAssetReference RewriteFileSystemMaterialReference(SceneAssetReference reference, string buildRootPath) {
            string fullPath = ResolveProjectAssetPath(reference.RelativePath);
            MaterialAsset materialAsset = ProjectContentManager.Load<MaterialAsset>(fullPath, EditorContentProcessorIds.MaterialAsset);
            if (MaterialBuilder != null) {
                AssetImportSettings materialSettings = LoadMaterialSettingsForCook(fullPath, reference.RelativePath, materialAsset);
                PlatformMaterialCookResult cookResult = MaterialBuilder.CookMaterial(BuildMaterialCookRequest(reference, materialAsset, materialSettings));
                RememberReferencedShaderAssetIds(cookResult.ReferencedShaderAssetIds);

                string cookedRelativePath = NormalizeRelativePath(reference.RelativePath);
                WriteBytes(Path.Combine(buildRootPath, cookedRelativePath), cookResult.CookedMaterialBytes);
                return CreateFileSystemReference(cookedRelativePath);
            }

            RememberReferencedShaderAssetId(materialAsset.ShaderAssetId);

            string relativePath = NormalizeRelativePath(reference.RelativePath);
            CopyFile(fullPath, Path.Combine(buildRootPath, relativePath));
            return CreateFileSystemReference(relativePath);
        }

        /// <summary>
        /// Loads one material-settings sidecar for build-time material translation.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset.</param>
        /// <param name="materialRelativePath">Project-relative material asset path used in diagnostics.</param>
        /// <returns>Deserialized material settings sidecar.</returns>
        AssetImportSettings LoadMaterialSettingsForCook(string materialAssetPath, string materialRelativePath, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (string.IsNullOrWhiteSpace(materialRelativePath)) {
                throw new ArgumentException("Material relative path must be provided.", nameof(materialRelativePath));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            string settingsPath = materialAssetPath + AssetImportManager.SettingsExtension;
            if (File.Exists(settingsPath)) {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                AssetImportSettings settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                if (settings.Processor?.Platforms != null && settings.Processor.Platforms.ContainsKey(TargetPlatformId)) {
                    return settings;
                }
            }

            if (MaterialBuilder != null) {
                return MaterialAssetSettingsService.LoadOrCreate(
                    materialAssetPath,
                    materialAsset,
                    [TargetPlatformId],
                    ResolveSelectionModelForMaterialSettings);
            }

            throw new InvalidOperationException($"Material '{materialRelativePath}' is missing settings required for target platform '{TargetPlatformId}'.");
        }

        /// <summary>
        /// Resolves the builder selection model used to seed missing material settings during scene-component packaging.
        /// </summary>
        /// <param name="platformId">Platform whose builder metadata should be returned.</param>
        /// <returns>Selection model backed by the active material builder definition.</returns>
        EditorPlatformBuildSelectionModel ResolveSelectionModelForMaterialSettings(string platformId) {
            if (!string.Equals(platformId, TargetPlatformId, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Material settings were requested for unexpected platform '{platformId}'.");
            } else if (MaterialBuilder == null) {
                throw new InvalidOperationException("Material builder must exist before resolving material settings metadata.");
            }

            return EditorPlatformBuildSelectionModel.From(MaterialBuilder.Definition);
        }

        /// <summary>
        /// Builds one builder-owned material translation request from the target-platform sidecar settings.
        /// </summary>
        /// <param name="reference">File-backed material reference being packaged.</param>
        /// <param name="materialAsset">Source serialized material asset.</param>
        /// <param name="materialSettings">Per-platform material settings sidecar.</param>
        /// <returns>Builder-owned material translation request.</returns>
        PlatformMaterialCookRequest BuildMaterialCookRequest(
            SceneAssetReference reference,
            MaterialAsset materialAsset,
            AssetImportSettings materialSettings) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            }

            MaterialAssetProcessorSettings platformMaterialSettings = materialSettings.Processor.Platforms[TargetPlatformId].Material;
            if (platformMaterialSettings == null) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' is missing material settings for target platform '{TargetPlatformId}'.");
            } else if (string.IsNullOrWhiteSpace(platformMaterialSettings.SchemaId)) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' is missing a schema id for target platform '{TargetPlatformId}'.");
            }

            return new PlatformMaterialCookRequest(
                materialAsset.Id ?? reference.RelativePath,
                reference.RelativePath,
                TargetPlatformId,
                SelectedBuildProfileId,
                SelectedGraphicsProfileId,
                platformMaterialSettings.SchemaId,
                platformMaterialSettings.FieldValues);
        }

        void EnsureGeneratedStandardMaterialAssets(string buildRootPath) {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, StandardShaderFileName);
            WriteAsset(Path.Combine(buildRootPath, StandardGeneratedShaderRelativePath), shaderAsset);

            MaterialAsset materialAsset = new MaterialAsset {
                Id = "Engine.Materials.Standard.material",
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = StandardShaderVariantName
            };
            WriteAsset(Path.Combine(buildRootPath, StandardGeneratedMaterialRelativePath), materialAsset);
        }

        void RememberReferencedShaderAssetId(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new InvalidOperationException("Material assets used by packaged scenes must include a shader asset id.");
            }

            if (ReferencedShaderAssetIdsSet.Add(shaderAssetId)) {
                ReferencedShaderAssetIds.Add(shaderAssetId);
            }
        }

        /// <summary>
        /// Tracks referenced shader asset ids returned by one builder-owned material cook result.
        /// </summary>
        /// <param name="shaderAssetIds">Referenced shader asset ids to record.</param>
        void RememberReferencedShaderAssetIds(IReadOnlyList<string> shaderAssetIds) {
            if (shaderAssetIds == null) {
                throw new ArgumentNullException(nameof(shaderAssetIds));
            }

            for (int index = 0; index < shaderAssetIds.Count; index++) {
                RememberReferencedShaderAssetId(shaderAssetIds[index]);
            }
        }

        SceneAssetReference CreateFileSystemReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = NormalizeRelativePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        string ResolveProjectAssetPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, normalizedRelativePath));
            string assetsRootPrefix = EnsureTrailingDirectorySeparator(AssetsRootPath);
            if (!fullPath.StartsWith(assetsRootPrefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Project asset paths must stay inside the source assets folder.");
            }

            return fullPath;
        }

        string BuildImportedModelRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string changedExtensionPath = Path.ChangeExtension(normalizedRelativePath, ".hasset");
            return NormalizeRelativePath(Path.Combine("cooked", "imported", changedExtensionPath));
        }

        SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

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

        void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }

        float4 ReadFloat4(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        CameraClearSettings ReadClearSettings(EngineBinaryReader reader) {
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

        void WriteClearSettings(EngineBinaryWriter writer, CameraClearSettings settings) {
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

        CameraRenderSettings ReadRenderSettings(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new CameraRenderSettings {
                DepthPrepassMode = (DepthPrepassMode)reader.ReadByte(),
                ShadowDistance = reader.ReadSingle(),
                PostProcessTier = (PostProcessTier)reader.ReadByte()
            };
        }

        void WriteRenderSettings(EngineBinaryWriter writer, CameraRenderSettings settings) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            writer.WriteByte((byte)settings.DepthPrepassMode);
            writer.WriteSingle(settings.ShadowDistance);
            writer.WriteByte((byte)settings.PostProcessTier);
        }

        ushort NormalizePackagedCameraLayerMask(ushort layerMask) {
            return RuntimeSceneLayerMask;
        }

        void WriteAsset(string fullPath, Asset asset) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(fullPath));
            }
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, asset);
        }

        /// <summary>
        /// Writes one serialized asset payload to disk from prebuilt bytes.
        /// </summary>
        /// <param name="fullPath">Absolute output path.</param>
        /// <param name="data">Serialized asset bytes to write.</param>
        void WriteBytes(string fullPath, byte[] data) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(fullPath));
            } else if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.WriteAllBytes(fullPath, data);
        }

        void WriteFontAsset(string fullPath, FontAsset fontAsset) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(fullPath));
            }
            if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Output directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        void CopyFile(string sourcePath, string targetPath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (string.IsNullOrWhiteSpace(targetPath)) {
                throw new ArgumentException("Target path must be provided.", nameof(targetPath));
            }

            string directoryPath = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Copy target directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            File.Copy(sourcePath, targetPath, true);
        }

        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/');
        }

        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
