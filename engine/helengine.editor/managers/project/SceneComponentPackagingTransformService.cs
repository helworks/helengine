using helengine.baseplatform.Builders;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Rewrites shared scene component payloads into packaged runtime forms.
    /// </summary>
    public sealed class SceneComponentPackagingTransformService {
        /// <summary>
        /// Current payload version for serialized mesh component scene records.
        /// </summary>
        const byte MeshComponentPayloadVersion = MeshComponentScenePayloadSerializer.CurrentVersion;

        /// <summary>
        /// Current payload version for serialized camera component scene records.
        /// </summary>
        const byte CameraComponentPayloadVersion = 3;

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
        /// Stable tagged field name used for mesh model-reference persistence.
        /// </summary>
        const string MeshModelReferenceFieldName = "ModelReference";

        /// <summary>
        /// Stable tagged field name used for mesh material-reference persistence.
        /// </summary>
        const string MeshMaterialReferenceFieldName = "MaterialReference";

        /// <summary>
        /// Stable tagged field name used for mesh material-reference array persistence.
        /// </summary>
        const string MeshMaterialReferencesFieldName = "MaterialReferences";

        /// <summary>
        /// Stable tagged field name used for mesh render-order persistence.
        /// </summary>
        const string MeshRenderOrder3DFieldName = "RenderOrder3D";

        /// <summary>
        /// Stable serialized component id for camera components.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";

        /// <summary>
        /// Stable tagged field name used for camera draw-order persistence.
        /// </summary>
        const string CameraDrawOrderFieldName = "CameraDrawOrder";

        /// <summary>
        /// Stable tagged field name used for camera layer-mask persistence.
        /// </summary>
        const string CameraLayerMaskFieldName = "LayerMask";

        /// <summary>
        /// Stable tagged field name used for camera viewport persistence.
        /// </summary>
        const string CameraViewportFieldName = "Viewport";

        /// <summary>
        /// Stable tagged field name used for camera near clip-plane persistence.
        /// </summary>
        const string CameraNearPlaneDistanceFieldName = "NearPlaneDistance";

        /// <summary>
        /// Stable tagged field name used for camera far clip-plane persistence.
        /// </summary>
        const string CameraFarPlaneDistanceFieldName = "FarPlaneDistance";

        /// <summary>
        /// Stable tagged field name used for camera clear-settings persistence.
        /// </summary>
        const string CameraClearSettingsFieldName = "ClearSettings";

        /// <summary>
        /// Stable tagged field name used for camera render-settings persistence.
        /// </summary>
        const string CameraRenderSettingsFieldName = "RenderSettings";

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
        /// Authored city-script type id for directional-shadow camera orbit motion.
        /// </summary>
        const string CityDirectionalShadowCameraOrbitComponentTypeId = "city.rendering.DirectionalShadowCameraOrbitComponent, gameplay";

        /// <summary>
        /// Authored gameplay-script type id for directional-shadow camera orbit motion.
        /// </summary>
        const string GameplayDirectionalShadowCameraOrbitComponentTypeId = "gameplay.rendering.DirectionalShadowCameraOrbitComponent, gameplay";

        /// <summary>
        /// Authored city-script type id for directional-shadow orbit motion.
        /// </summary>
        const string CityDirectionalShadowOrbitComponentTypeId = "city.rendering.DirectionalShadowOrbitComponent, gameplay";

        /// <summary>
        /// Authored gameplay-script type id for directional-shadow orbit motion.
        /// </summary>
        const string GameplayDirectionalShadowOrbitComponentTypeId = "gameplay.rendering.DirectionalShadowOrbitComponent, gameplay";

        /// <summary>
        /// Authored city-script type id for directional-shadow sun sweep motion.
        /// </summary>
        const string CityDirectionalShadowSunSweepComponentTypeId = "city.rendering.DirectionalShadowSunSweepComponent, gameplay";

        /// <summary>
        /// Authored gameplay-script type id for directional-shadow sun sweep motion.
        /// </summary>
        const string GameplayDirectionalShadowSunSweepComponentTypeId = "gameplay.rendering.DirectionalShadowSunSweepComponent, gameplay";

        /// <summary>
        /// Authored city-script type id for directional-shadow tower spin motion.
        /// </summary>
        const string CityDirectionalShadowTowerSpinComponentTypeId = "city.rendering.DirectionalShadowTowerSpinComponent, gameplay";

        /// <summary>
        /// Authored gameplay-script type id for directional-shadow tower spin motion.
        /// </summary>
        const string GameplayDirectionalShadowTowerSpinComponentTypeId = "gameplay.rendering.DirectionalShadowTowerSpinComponent, gameplay";

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
        /// Stable generated model asset id for the built-in sphere primitive.
        /// </summary>
        const string SphereGeneratedAssetId = "engine:model:sphere";

        /// <summary>
        /// Stable generated material asset id for the built-in standard material.
        /// </summary>
        const string StandardGeneratedMaterialAssetId = "engine:material:standard";
        /// <summary>
        /// Folder name used for packaged imported texture assets referenced by material albedo bindings.
        /// </summary>
        const string ImportedTextureDirectoryName = "cooked/imported";

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
        /// Stable shader asset identifier used by the packaged standard material.
        /// </summary>
        const string StandardShaderAssetId = "ForwardStandardShader";

        /// <summary>
        /// Field id used to toggle custom shader overrides in the material schema.
        /// </summary>
        const string UseCustomShaderFieldId = "use-custom-shader";

        /// <summary>
        /// Field id used for the shader asset identifier in the material schema.
        /// </summary>
        const string ShaderAssetIdFieldId = "shader-asset-id";

        /// <summary>
        /// Field id used for the vertex program identifier in the material schema.
        /// </summary>
        const string VertexProgramFieldId = "vertex-program";

        /// <summary>
        /// Field id used for the pixel program identifier in the material schema.
        /// </summary>
        const string PixelProgramFieldId = "pixel-program";

        /// <summary>
        /// Field id used for the mesh-derived variant forwarded to the builder.
        /// </summary>
        const string VariantFieldId = "variant";

        /// <summary>
        /// Schema id used by the standard Windows material path.
        /// </summary>
        const string StandardShaderSchemaId = "standard-shader";

        /// <summary>
        /// Mesh-derived material variant used by scene packaging.
        /// </summary>
        const string MeshVariantName = "Mesh";

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
        /// Reflected component schema builder used for automatic ordinal payload rewrites.
        /// </summary>
        readonly ScriptComponentReflectionSchemaBuilder ScriptComponentSchemaBuilder;

        /// <summary>
        /// Optional shared script type resolver used for loaded gameplay modules.
        /// </summary>
        readonly IScriptTypeResolver ScriptTypeResolver;

        /// <summary>
        /// Automatic reflected-component descriptor used to interpret editor tagged payloads before packaging.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticScriptComponentDescriptor;

        /// <summary>
        /// Shared persistence registry used to resolve explicit descriptors and the automatic reflected fallback by serialized component type id.
        /// </summary>
        readonly ComponentPersistenceRegistry PersistenceRegistry;
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
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
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
            string selectedGraphicsProfileId = "",
            IScriptTypeResolver scriptTypeResolver = null) {
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
            ScriptTypeResolver = scriptTypeResolver;
            AutomaticScriptComponentDescriptor = new AutomaticScriptComponentPersistenceDescriptor(ScriptComponentSchemaBuilder, scriptTypeResolver);
            CameraComponentDescriptor = new CameraComponentPersistenceDescriptor();
            RoundedRectComponentDescriptor = new RoundedRectComponentPersistenceDescriptor();
            DemoMenuBuildComponentDescriptor = new MenuComponentPersistenceDescriptor();
            DemoMenuPanelComponentDescriptor = new MenuPanelComponentPersistenceDescriptor();
            DemoMenuItemComponentDescriptor = new MenuItemComponentPersistenceDescriptor();
            DemoMenuSelectedDescriptionComponentDescriptor = new MenuSelectedDescriptionComponentPersistenceDescriptor();
            PersistenceRegistry = new ComponentPersistenceRegistry(scriptTypeResolver);
            PersistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            PersistenceRegistry.Register(CameraComponentDescriptor);
            PersistenceRegistry.Register(new TextComponentPersistenceDescriptor());
            PersistenceRegistry.Register(RoundedRectComponentDescriptor);
            PersistenceRegistry.Register(new FPSComponentPersistenceDescriptor());
            PersistenceRegistry.Register(new DirectionalLightComponentPersistenceDescriptor());
            PersistenceRegistry.Register(new PointLightComponentPersistenceDescriptor());
            PersistenceRegistry.Register(new SpotLightComponentPersistenceDescriptor());
            PersistenceRegistry.Register(DemoMenuBuildComponentDescriptor);
            PersistenceRegistry.Register(DemoMenuPanelComponentDescriptor);
            PersistenceRegistry.Register(DemoMenuItemComponentDescriptor);
            PersistenceRegistry.Register(DemoMenuSelectedDescriptionComponentDescriptor);
        }

        /// <summary>
        /// Returns whether the service can rewrite the supplied serialized component type id into packaged runtime form.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <returns>True when the service can rewrite the component; otherwise false.</returns>
        public bool CanTransform(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return false;
            }

            if (string.Equals(componentTypeId, MeshComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, CameraComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, FPSComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, TextComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, RoundedRectComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, DirectionalLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, PointLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, SpotLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, MenuComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, MenuPanelComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, MenuItemComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, MenuSelectedDescriptionComponent.SerializedComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return TryResolvePersistenceDescriptor(componentTypeId, out _);
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
                transformedRecord = RewriteFPSComponentRecord(record, buildRootPath);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, TextComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteTextComponentRecord(record, buildRootPath);
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

            if (TryRewriteDirectionalShadowMotionComponentRecord(record, out transformedRecord)) {
                return true;
            }

            if (TryRewriteAutomaticComponentRecord(record, out transformedRecord)) {
                return true;
            }

            transformedRecord = null;
            return false;
        }

        /// <summary>
        /// Attempts to resolve one persistence descriptor by serialized component type id.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <param name="descriptor">Resolved descriptor when available.</param>
        /// <returns>True when a descriptor or automatic fallback can resolve the type id.</returns>
        bool TryResolvePersistenceDescriptor(string componentTypeId, out IComponentPersistenceDescriptor descriptor) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                descriptor = null;
                return false;
            }

            try {
                descriptor = PersistenceRegistry.GetDescriptor(componentTypeId);
                return descriptor != null;
            } catch (InvalidOperationException) {
                descriptor = null;
                return false;
            }
        }

        /// <summary>
        /// Rewrites one authored directional-shadow motion script component into its built-in player component counterpart when supported.
        /// </summary>
        /// <param name="record">Serialized component record to rewrite.</param>
        /// <param name="transformedRecord">Rewritten component record when successful.</param>
        /// <returns>True when the record was rewritten to a built-in player component; otherwise false.</returns>
        bool TryRewriteDirectionalShadowMotionComponentRecord(SceneComponentAssetRecord record, out SceneComponentAssetRecord transformedRecord) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.Equals(record.ComponentTypeId, CityDirectionalShadowCameraOrbitComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.ComponentTypeId, GameplayDirectionalShadowCameraOrbitComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDirectionalShadowCameraOrbitComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, CityDirectionalShadowOrbitComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.ComponentTypeId, GameplayDirectionalShadowOrbitComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDirectionalShadowOrbitComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, CityDirectionalShadowSunSweepComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.ComponentTypeId, GameplayDirectionalShadowSunSweepComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDirectionalShadowSunSweepComponentRecord(record);
                return true;
            }

            if (string.Equals(record.ComponentTypeId, CityDirectionalShadowTowerSpinComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.ComponentTypeId, GameplayDirectionalShadowTowerSpinComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = RewriteDirectionalShadowTowerSpinComponentRecord(record);
                return true;
            }

            transformedRecord = null;
            return false;
        }

        /// <summary>
        /// Rewrites one named editor reflected-component payload into the strict packaged ordinal payload shape.
        /// </summary>
        /// <param name="record">Serialized component record to rewrite.</param>
        /// <param name="transformedRecord">Rewritten component record when successful.</param>
        /// <returns>True when the record was rewritten through the automatic reflected fallback; otherwise false.</returns>
        bool TryRewriteAutomaticComponentRecord(SceneComponentAssetRecord record, out SceneComponentAssetRecord transformedRecord) {
            if (!TryResolvePersistenceDescriptor(record.ComponentTypeId, out IComponentPersistenceDescriptor descriptor)) {
                transformedRecord = null;
                return false;
            }

            Component component = DeserializeAutomaticComponentForPackaging(record, descriptor);
            ScriptComponentReflectionSchema schema = ScriptComponentSchemaBuilder.Build(component.GetType());

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedValue(writer, member.ValueType, member.GetValue(component));
            }

            transformedRecord = new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };

            return true;
        }

        /// <summary>
        /// Deserializes one automatic scripted component for packaging through its resolved persistence descriptor.
        /// </summary>
        /// <param name="record">Serialized component record being packaged.</param>
        /// <param name="descriptor">Resolved persistence descriptor for the component type.</param>
        /// <returns>Live component instance ready for runtime payload emission.</returns>
        Component DeserializeAutomaticComponentForPackaging(SceneComponentAssetRecord record, IComponentPersistenceDescriptor descriptor) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            return descriptor.DeserializeComponent(record, null, null);
        }

        /// <summary>
        /// Rewrites one authored directional-shadow camera-orbit script record into the built-in player component form.
        /// </summary>
        /// <param name="record">Serialized authored script record to rewrite.</param>
        /// <returns>Rewritten player component record.</returns>
        SceneComponentAssetRecord RewriteDirectionalShadowCameraOrbitComponentRecord(SceneComponentAssetRecord record) {
            Component sourceComponent = DeserializeAuthoredDirectionalShadowComponent(record);
            DirectionalShadowCameraOrbitComponent component = new DirectionalShadowCameraOrbitComponent {
                OrbitCenter = ReadRequiredFloat3MemberValue(sourceComponent, "OrbitCenter"),
                OrbitRadius = ReadRequiredSingleMemberValue(sourceComponent, "OrbitRadius"),
                OrbitHeight = ReadRequiredSingleMemberValue(sourceComponent, "OrbitHeight"),
                BaseAngleRadians = ReadRequiredSingleMemberValue(sourceComponent, "BaseAngleRadians"),
                AngularSpeedRadians = ReadRequiredSingleMemberValue(sourceComponent, "AngularSpeedRadians"),
                LookDownPitchRadians = ReadRequiredSingleMemberValue(sourceComponent, "LookDownPitchRadians")
            };

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion);
            DirectionalShadowMotionComponentScenePayloadSerializer.WriteCameraOrbit(writer, component);
            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalShadowCameraOrbitComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one authored directional-shadow orbit script record into the built-in player component form.
        /// </summary>
        /// <param name="record">Serialized authored script record to rewrite.</param>
        /// <returns>Rewritten player component record.</returns>
        SceneComponentAssetRecord RewriteDirectionalShadowOrbitComponentRecord(SceneComponentAssetRecord record) {
            Component sourceComponent = DeserializeAuthoredDirectionalShadowComponent(record);
            DirectionalShadowOrbitComponent component = new DirectionalShadowOrbitComponent {
                OrbitCenter = ReadRequiredFloat3MemberValue(sourceComponent, "OrbitCenter"),
                OrbitRadius = ReadRequiredSingleMemberValue(sourceComponent, "OrbitRadius"),
                OrbitHeight = ReadRequiredSingleMemberValue(sourceComponent, "OrbitHeight"),
                BaseAngleRadians = ReadRequiredSingleMemberValue(sourceComponent, "BaseAngleRadians"),
                AngularSpeedRadians = ReadRequiredSingleMemberValue(sourceComponent, "AngularSpeedRadians")
            };

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion);
            DirectionalShadowMotionComponentScenePayloadSerializer.WriteOrbit(writer, component);
            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalShadowOrbitComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one authored directional-shadow sun-sweep script record into the built-in player component form.
        /// </summary>
        /// <param name="record">Serialized authored script record to rewrite.</param>
        /// <returns>Rewritten player component record.</returns>
        SceneComponentAssetRecord RewriteDirectionalShadowSunSweepComponentRecord(SceneComponentAssetRecord record) {
            Component sourceComponent = DeserializeAuthoredDirectionalShadowComponent(record);
            DirectionalShadowSunSweepComponent component = new DirectionalShadowSunSweepComponent {
                MinYawRadians = ReadRequiredSingleMemberValue(sourceComponent, "MinYawRadians"),
                MaxYawRadians = ReadRequiredSingleMemberValue(sourceComponent, "MaxYawRadians"),
                PitchRadians = ReadRequiredSingleMemberValue(sourceComponent, "PitchRadians"),
                SweepSpeedRadians = ReadRequiredSingleMemberValue(sourceComponent, "SweepSpeedRadians")
            };

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion);
            DirectionalShadowMotionComponentScenePayloadSerializer.WriteSunSweep(writer, component);
            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalShadowSunSweepComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one authored directional-shadow tower-spin script record into the built-in player component form.
        /// </summary>
        /// <param name="record">Serialized authored script record to rewrite.</param>
        /// <returns>Rewritten player component record.</returns>
        SceneComponentAssetRecord RewriteDirectionalShadowTowerSpinComponentRecord(SceneComponentAssetRecord record) {
            Component sourceComponent = DeserializeAuthoredDirectionalShadowComponent(record);
            DirectionalShadowTowerSpinComponent component = new DirectionalShadowTowerSpinComponent {
                BaseYawRadians = ReadRequiredSingleMemberValue(sourceComponent, "BaseYawRadians"),
                AngularSpeedRadians = ReadRequiredSingleMemberValue(sourceComponent, "AngularSpeedRadians")
            };

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(DirectionalShadowMotionComponentScenePayloadSerializer.CurrentVersion);
            DirectionalShadowMotionComponentScenePayloadSerializer.WriteTowerSpin(writer, component);
            return new SceneComponentAssetRecord {
                ComponentTypeId = DirectionalShadowTowerSpinComponent.SerializedComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one authored directional-shadow script component through the shared persistence registry.
        /// </summary>
        /// <param name="record">Serialized authored script record to materialize.</param>
        /// <returns>Materialized authored script component.</returns>
        Component DeserializeAuthoredDirectionalShadowComponent(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!TryResolvePersistenceDescriptor(record.ComponentTypeId, out IComponentPersistenceDescriptor descriptor)) {
                throw new InvalidOperationException($"No scene persistence descriptor is registered for '{record.ComponentTypeId}'.");
            }

            return descriptor.DeserializeComponent(record, null, null);
        }

        /// <summary>
        /// Reads one required public float member value from the supplied component instance.
        /// </summary>
        /// <param name="component">Component instance that owns the member.</param>
        /// <param name="memberName">Exact public member name to read.</param>
        /// <returns>Decoded float member value.</returns>
        static float ReadRequiredSingleMemberValue(Component component, string memberName) {
            object value = ReadRequiredMemberValue(component, memberName);
            if (value is not float floatValue) {
                throw new InvalidOperationException($"Component member '{component.GetType().FullName}.{memberName}' must be a float.");
            }

            return floatValue;
        }

        /// <summary>
        /// Reads one required public <see cref="float3"/> member value from the supplied component instance.
        /// </summary>
        /// <param name="component">Component instance that owns the member.</param>
        /// <param name="memberName">Exact public member name to read.</param>
        /// <returns>Decoded <see cref="float3"/> member value.</returns>
        static float3 ReadRequiredFloat3MemberValue(Component component, string memberName) {
            object value = ReadRequiredMemberValue(component, memberName);
            if (value is not float3 floatValue) {
                throw new InvalidOperationException($"Component member '{component.GetType().FullName}.{memberName}' must be a float3.");
            }

            return floatValue;
        }

        /// <summary>
        /// Reads one required public instance member value from the supplied component.
        /// </summary>
        /// <param name="component">Component instance that owns the member.</param>
        /// <param name="memberName">Exact public member name to read.</param>
        /// <returns>Member value read from the component.</returns>
        static object ReadRequiredMemberValue(Component component, string memberName) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            PropertyInfo propertyInfo = component.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (propertyInfo != null) {
                if (propertyInfo.GetMethod == null || !propertyInfo.GetMethod.IsPublic) {
                    throw new InvalidOperationException($"Component member '{component.GetType().FullName}.{memberName}' must expose a public getter.");
                }

                return propertyInfo.GetValue(component);
            }

            FieldInfo fieldInfo = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (fieldInfo != null) {
                return fieldInfo.GetValue(component);
            }

            throw new InvalidOperationException($"Component member '{component.GetType().FullName}.{memberName}' is required for directional-shadow packaging.");
        }

        /// <summary>
        /// Rewrites one serialized mesh payload into the strict runtime mesh payload shape.
        /// </summary>
        /// <param name="record">Serialized mesh component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten mesh component record.</returns>
        SceneComponentAssetRecord RewriteMeshComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            ReadMeshComponentRecord(
                record,
                out SceneAssetReference modelReference,
                out SceneAssetReference[] materialReferences,
                out byte renderOrder3D);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            MeshComponentScenePayloadSerializer.Write(
                writer,
                RewriteModelReference(modelReference, buildRootPath),
                RewriteMaterialReferences(materialReferences, buildRootPath),
                renderOrder3D);

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
            ReadCameraComponentRecord(
                record,
                out byte cameraDrawOrder,
                out ushort layerMask,
                out float4 viewport,
                out float nearPlaneDistance,
                out float farPlaneDistance,
                out CameraClearSettings clearSettings,
                out CameraRenderSettings renderSettings);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CameraComponentPayloadVersion);
            writer.WriteByte(cameraDrawOrder);
            writer.WriteUInt16(NormalizePackagedCameraLayerMask(layerMask));
            WriteFloat4(writer, viewport);
            writer.WriteSingle(nearPlaneDistance);
            writer.WriteSingle(farPlaneDistance);
            WriteClearSettings(writer, clearSettings);
            WriteRenderSettings(writer, renderSettings ?? new CameraRenderSettings());

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Reads one serialized mesh payload from the current tagged editor format.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <param name="modelReference">Persisted model reference.</param>
        /// <param name="materialReference">Persisted material reference.</param>
        /// <param name="renderOrder3D">Persisted render order.</param>
        void ReadMeshComponentRecord(
            SceneComponentAssetRecord record,
            out SceneAssetReference modelReference,
            out SceneAssetReference[] materialReferences,
            out byte renderOrder3D) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            ReadTaggedMeshComponentRecord(
                record,
                out modelReference,
                out materialReferences,
                out renderOrder3D);
        }

        /// <summary>
        /// Reads one serialized camera payload from the current tagged editor format.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <param name="cameraDrawOrder">Persisted camera draw order.</param>
        /// <param name="layerMask">Persisted camera layer mask.</param>
        /// <param name="viewport">Persisted camera viewport.</param>
        /// <param name="clearSettings">Persisted camera clear settings.</param>
        /// <param name="renderSettings">Persisted camera render settings.</param>
        void ReadCameraComponentRecord(
            SceneComponentAssetRecord record,
            out byte cameraDrawOrder,
            out ushort layerMask,
            out float4 viewport,
            out float nearPlaneDistance,
            out float farPlaneDistance,
            out CameraClearSettings clearSettings,
            out CameraRenderSettings renderSettings) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            ReadTaggedCameraComponentRecord(
                record,
                out cameraDrawOrder,
                out layerMask,
                out viewport,
                out nearPlaneDistance,
                out farPlaneDistance,
                out clearSettings,
                out renderSettings);
        }

        /// <summary>
        /// Rewrites one serialized FPS payload into the strict runtime FPS payload shape.
        /// </summary>
        /// <param name="record">Serialized FPS component record to rewrite.</param>
        /// <returns>Rewritten FPS component record.</returns>
        SceneComponentAssetRecord RewriteFPSComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != FPSComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported FPS component payload version '{version}'.");
            }

            SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ReadOptionalReference(reader);
            double refreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64());
            int2 padding = reader.ReadInt2();
            byte renderOrder2D = reader.ReadByte();

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(FPSComponentPayloadVersion);
            WriteOptionalReference(writer, RewriteFontReference(fontReference, buildRootPath));
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
        SceneComponentAssetRecord RewriteTextComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
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
            WriteOptionalReference(writer, RewriteFontReference(fontReference, buildRootPath));
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
            Component component = new DirectionalLightComponentPersistenceDescriptor().DeserializeComponent(record, null, null);
            if (component is not DirectionalLightComponent lightComponent) {
                throw new InvalidOperationException($"Expected directional light descriptor to materialize '{DirectionalLightComponentTypeId}'.");
            }

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
            Component component = new PointLightComponentPersistenceDescriptor().DeserializeComponent(record, null, null);
            if (component is not PointLightComponent lightComponent) {
                throw new InvalidOperationException($"Expected point light descriptor to materialize '{PointLightComponentTypeId}'.");
            }

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
            Component component = new SpotLightComponentPersistenceDescriptor().DeserializeComponent(record, null, null);
            if (component is not SpotLightComponent lightComponent) {
                throw new InvalidOperationException($"Expected spot light descriptor to materialize '{SpotLightComponentTypeId}'.");
            }

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
        /// Reads one tagged camera payload into the runtime values needed for packaged rewriting without constructing a live camera component.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <param name="cameraDrawOrder">Persisted camera draw order.</param>
        /// <param name="layerMask">Persisted camera layer mask.</param>
        /// <param name="viewport">Persisted camera viewport.</param>
        /// <param name="clearSettings">Persisted camera clear settings.</param>
        /// <param name="renderSettings">Persisted camera render settings.</param>
        void ReadTaggedCameraComponentRecord(
            SceneComponentAssetRecord record,
            out byte cameraDrawOrder,
            out ushort layerMask,
            out float4 viewport,
            out float nearPlaneDistance,
            out float farPlaneDistance,
            out CameraClearSettings clearSettings,
            out CameraRenderSettings renderSettings) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, CameraComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Expected camera record but received '{record.ComponentTypeId}'.");
            }

            cameraDrawOrder = 0;
            layerMask = 0b11111111;
            viewport = new float4(0f, 0f, 1f, 1f);
            nearPlaneDistance = 0.1f;
            farPlaneDistance = 100f;
            clearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            renderSettings = new CameraRenderSettings();

            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());

            if (reader.TryGetFieldReader(CameraDrawOrderFieldName, out EngineBinaryReader cameraDrawOrderReader)) {
                using (cameraDrawOrderReader) {
                    cameraDrawOrder = cameraDrawOrderReader.ReadByte();
                }
            }

            if (reader.TryGetFieldReader(CameraLayerMaskFieldName, out EngineBinaryReader layerMaskReader)) {
                using (layerMaskReader) {
                    layerMask = layerMaskReader.ReadUInt16();
                }
            }

            if (reader.TryGetFieldReader(CameraViewportFieldName, out EngineBinaryReader viewportReader)) {
                using (viewportReader) {
                    viewport = viewportReader.ReadFloat4();
                }
            }

            if (reader.TryGetFieldReader(CameraNearPlaneDistanceFieldName, out EngineBinaryReader nearPlaneDistanceReader)) {
                using (nearPlaneDistanceReader) {
                    nearPlaneDistance = nearPlaneDistanceReader.ReadSingle();
                }
            }

            if (reader.TryGetFieldReader(CameraFarPlaneDistanceFieldName, out EngineBinaryReader farPlaneDistanceReader)) {
                using (farPlaneDistanceReader) {
                    farPlaneDistance = farPlaneDistanceReader.ReadSingle();
                }
            }

            if (reader.TryGetFieldReader(CameraClearSettingsFieldName, out EngineBinaryReader clearSettingsReader)) {
                using (clearSettingsReader) {
                    clearSettings = SceneComponentBinaryFieldEncoding.ReadCameraClearSettings(clearSettingsReader);
                }
            }

            if (reader.TryGetFieldReader(CameraRenderSettingsFieldName, out EngineBinaryReader renderSettingsReader)) {
                using (renderSettingsReader) {
                    renderSettings = SceneComponentBinaryFieldEncoding.ReadCameraRenderSettings(renderSettingsReader);
                }
            }
        }

        /// <summary>
        /// Reads one tagged mesh payload into the authored asset references and render order needed for packaged rewriting.
        /// </summary>
        /// <param name="record">Scene component record to interpret.</param>
        /// <param name="modelReference">Persisted model reference.</param>
        /// <param name="materialReferences">Persisted material references ordered by submesh slot.</param>
        /// <param name="renderOrder3D">Persisted render order.</param>
        void ReadTaggedMeshComponentRecord(
            SceneComponentAssetRecord record,
            out SceneAssetReference modelReference,
            out SceneAssetReference[] materialReferences,
            out byte renderOrder3D) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, MeshComponentTypeId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Expected mesh record but received '{record.ComponentTypeId}'.");
            }

            modelReference = null;
            materialReferences = Array.Empty<SceneAssetReference>();
            renderOrder3D = 0;

            EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());

            if (reader.TryGetFieldReader(MeshModelReferenceFieldName, out EngineBinaryReader modelReferenceReader)) {
                using (modelReferenceReader) {
                    modelReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(modelReferenceReader);
                }
            }

            if (!reader.TryGetFieldReader(MeshMaterialReferencesFieldName, out EngineBinaryReader materialReferencesReader)) {
                throw new InvalidOperationException("Mesh component payload must include MaterialReferences.");
            }

            using (materialReferencesReader) {
                materialReferences = SceneComponentBinaryFieldEncoding.ReadOptionalReferenceArray(materialReferencesReader);
            }

            if (reader.TryGetFieldReader(MeshRenderOrder3DFieldName, out EngineBinaryReader renderOrder3DReader)) {
                using (renderOrder3DReader) {
                    renderOrder3D = renderOrder3DReader.ReadByte();
                }
            }
        }

        /// <summary>
        /// Rewrites one ordered material-reference array into packaged file-backed material references.
        /// </summary>
        /// <param name="materialReferences">Authored material references ordered by submesh slot.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged material references ordered by submesh slot.</returns>
        SceneAssetReference[] RewriteMaterialReferences(SceneAssetReference[] materialReferences, string buildRootPath) {
            if (materialReferences == null) {
                throw new ArgumentNullException(nameof(materialReferences));
            }

            SceneAssetReference[] rewrittenReferences = new SceneAssetReference[materialReferences.Length];
            for (int materialIndex = 0; materialIndex < materialReferences.Length; materialIndex++) {
                rewrittenReferences[materialIndex] = RewriteMaterialReference(materialReferences[materialIndex], buildRootPath);
            }

            return rewrittenReferences;
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

        SceneAssetReference RewriteFontReference(SceneAssetReference reference, string buildRootPath) {
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
                return RewriteFileSystemFontReference(reference, buildRootPath);
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

        /// <summary>
        /// Rewrites one file-backed source font reference into a cooked packaged font reference.
        /// </summary>
        /// <param name="reference">Serialized font reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed font reference.</returns>
        SceneAssetReference RewriteFileSystemFontReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            string cookedRelativePath = BuildCookedFontRelativePath(reference.RelativePath);
            if (string.Equals(Path.GetExtension(reference.RelativePath), ".hefont", StringComparison.OrdinalIgnoreCase)) {
                CopyFile(sourcePath, Path.Combine(buildRootPath, cookedRelativePath));
                return CreateFontFileReference(cookedRelativePath);
            }

            FontAsset fontAsset = LoadImportedFontAssetForPackaging(reference, sourcePath);
            WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), fontAsset);
            return CreateFontFileReference(cookedRelativePath);
        }

        /// <summary>
        /// Loads one imported source-font asset for packaging after the importer has produced a packaged `FontAsset`.
        /// </summary>
        /// <param name="reference">Serialized font reference being packaged.</param>
        /// <param name="sourcePath">Absolute project path resolved from the scene reference.</param>
        /// <returns>Loaded font asset ready to be written into the package.</returns>
        FontAsset LoadImportedFontAssetForPackaging(SceneAssetReference reference, string sourcePath) {
            if (AssetImportManager.TryLoadFontAsset(sourcePath, out FontAsset importedFontAsset)) {
                return importedFontAsset;
            }

            throw new InvalidOperationException($"Font source '{reference.RelativePath}' could not be imported for packaging.");
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

            if (string.Equals(reference.AssetId, SphereGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "cooked/engine/models/sphere.hasset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GenerateSphereMesh(float3.Zero, float3.One));
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
            MaterialAsset materialAsset;
            try {
                materialAsset = ProjectContentManager.Load<MaterialAsset>(fullPath, EditorContentProcessorIds.MaterialAsset);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' at '{fullPath}' could not be loaded for packaging.", ex);
            }
            string cookedRelativePath = BuildCookedMaterialRelativePath(reference.RelativePath);
            if (MaterialBuilder != null) {
                AssetImportSettings materialSettings = LoadMaterialSettingsForCook(fullPath, reference.RelativePath, materialAsset);
                PlatformMaterialCookRequest cookRequest = BuildMaterialCookRequest(reference, materialAsset, materialSettings);
                PlatformMaterialCookResult cookResult = MaterialBuilder.CookMaterial(cookRequest);
                RememberReferencedShaderAssetIds(cookResult.ReferencedShaderAssetIds);

                CopyReferencedDiffuseTextureAsset(fullPath, ResolveReferencedDiffuseTextureAssetId(materialAsset, cookRequest.FieldValues), buildRootPath);
                WriteBytes(Path.Combine(buildRootPath, cookedRelativePath), cookResult.CookedMaterialBytes);
                return CreateFileSystemReference(cookedRelativePath);
            }

            AssetImportSettings compatibilityMaterialSettings;
            if (MaterialAssetSettingsService.TryLoad(fullPath, out compatibilityMaterialSettings) &&
                HasValidPlatformMaterialSettings(compatibilityMaterialSettings, TargetPlatformId)) {
                MaterialAssetSettingsService.ApplyPlatformCompatibilityFields(materialAsset, compatibilityMaterialSettings, TargetPlatformId);
            }

            RememberReferencedShaderAssetId(materialAsset.ShaderAssetId);
            CopyReferencedDiffuseTextureAsset(fullPath, materialAsset, buildRootPath);

            WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), materialAsset);
            return CreateFileSystemReference(cookedRelativePath);
        }

        /// <summary>
        /// Copies one imported diffuse texture asset referenced by a material into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose diffuse texture should be packaged.</param>
        /// <param name="materialAsset">Material asset whose imported diffuse texture should be packaged.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedDiffuseTextureAsset(string materialAssetPath, MaterialAsset materialAsset, string buildRootPath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            CopyReferencedDiffuseTextureAsset(materialAssetPath, materialAsset.DiffuseTextureAssetId, buildRootPath);
        }

        /// <summary>
        /// Copies one imported diffuse texture asset referenced by a cooked material request into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose diffuse texture should be packaged.</param>
        /// <param name="diffuseTextureAssetId">Imported diffuse texture asset id that should be copied.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedDiffuseTextureAsset(string materialAssetPath, string diffuseTextureAssetId, string buildRootPath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(diffuseTextureAssetId)) {
                return;
            }

            TextureAsset textureAsset;
            string sourcePath;
            if (TryResolveSourceTextureAsset(materialAssetPath, diffuseTextureAssetId, out textureAsset, out sourcePath)) {
                string sourceCookedRelativePath = BuildImportedTextureCookedRelativePath(diffuseTextureAssetId);
                WriteAsset(Path.Combine(buildRootPath, sourceCookedRelativePath), textureAsset);
                return;
            }

            sourcePath = ResolveImportedTextureAssetPath(diffuseTextureAssetId);
            try {
                textureAsset = ProjectContentManager.Load<TextureAsset>(sourcePath, EditorContentProcessorIds.TextureAsset);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Imported texture '{diffuseTextureAssetId}' at '{sourcePath}' could not be loaded for packaging.", ex);
            }
            string cookedRelativePath = BuildImportedTextureCookedRelativePath(diffuseTextureAssetId);
            WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), textureAsset);
        }

        /// <summary>
        /// Attempts to resolve one legacy diffuse texture reference through the authored source tree instead of the cache.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset.</param>
        /// <param name="diffuseTextureAssetId">Diffuse texture asset id stored on the material asset.</param>
        /// <param name="textureAsset">Resolved source texture asset when available.</param>
        /// <param name="sourcePath">Absolute source texture path when available.</param>
        /// <returns>True when the diffuse texture resolved through the authored source tree; otherwise false.</returns>
        bool TryResolveSourceTextureAsset(string materialAssetPath, string diffuseTextureAssetId, out TextureAsset textureAsset, out string sourcePath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (string.IsNullOrWhiteSpace(diffuseTextureAssetId)) {
                textureAsset = null;
                sourcePath = string.Empty;
                return false;
            }

            if (!TryResolveSourceTexturePath(materialAssetPath, diffuseTextureAssetId, out sourcePath)) {
                textureAsset = null;
                return false;
            }
            if (!AssetImportManager.TryLoadTextureAsset(sourcePath, out textureAsset) || textureAsset == null) {
                throw new InvalidOperationException($"Texture source file '{sourcePath}' could not be imported for packaging.");
            }

            return true;
        }

        /// <summary>
        /// Resolves the diffuse texture asset id that one builder-backed material request will require at runtime.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as the fallback source.</param>
        /// <param name="fieldValues">Final builder field values prepared for cooking.</param>
        /// <returns>Imported diffuse texture asset id that should be copied, or an empty string when the material has no imported texture.</returns>
        static string ResolveReferencedDiffuseTextureAssetId(MaterialAsset materialAsset, IReadOnlyDictionary<string, string> fieldValues) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (fieldValues != null &&
                fieldValues.TryGetValue("texture-id", out string diffuseTextureAssetId) &&
                !string.IsNullOrWhiteSpace(diffuseTextureAssetId)) {
                return diffuseTextureAssetId;
            }

            return materialAsset.DiffuseTextureAssetId ?? string.Empty;
        }

        /// <summary>
        /// Resolves one imported texture asset id to the serialized cache file produced by the project asset importer.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the material asset.</param>
        /// <returns>Absolute path to the serialized cached texture asset.</returns>
        string ResolveImportedTextureAssetPath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string projectRootPath = Path.GetDirectoryName(AssetsRootPath);
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("Project root path could not be resolved from the assets root.");
            }

            return Path.Combine(projectRootPath, "cache", assetId);
        }

        /// <summary>
        /// Resolves one legacy diffuse texture identifier through the authored material directory, imported-model source directory, or project assets root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset.</param>
        /// <param name="assetId">Diffuse texture asset identifier stored on the material asset.</param>
        /// <param name="texturePath">Resolved absolute texture source path when available.</param>
        /// <returns>True when a matching source texture exists; otherwise false.</returns>
        bool TryResolveSourceTexturePath(string materialAssetPath, string assetId, out string texturePath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                texturePath = string.Empty;
                return false;
            }

            if (TryResolveTexturePathRelativeToDirectory(Path.GetDirectoryName(Path.GetFullPath(materialAssetPath)), assetId, out texturePath)) {
                return true;
            }
            if (TryResolveTexturePathRelativeToImportedModelSource(materialAssetPath, assetId, out texturePath)) {
                return true;
            }
            if (TryResolveTexturePathRelativeToDirectory(AssetsRootPath, assetId, out texturePath)) {
                return true;
            }

            texturePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Attempts to resolve one texture identifier relative to the imported-model source directory that owns the authored companion material.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored companion material asset.</param>
        /// <param name="assetId">Diffuse texture asset identifier stored on the material asset.</param>
        /// <param name="texturePath">Resolved absolute texture source path when available.</param>
        /// <returns>True when a matching texture exists beside the imported model source; otherwise false.</returns>
        bool TryResolveTexturePathRelativeToImportedModelSource(string materialAssetPath, string assetId, out string texturePath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                texturePath = string.Empty;
                return false;
            }

            string materialDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(materialAssetPath));
            if (string.IsNullOrWhiteSpace(materialDirectoryPath)) {
                texturePath = string.Empty;
                return false;
            }

            string modelDirectoryName = Path.GetFileName(materialDirectoryPath);
            string modelSourceRootPath = Path.GetDirectoryName(materialDirectoryPath);
            if (string.IsNullOrWhiteSpace(modelDirectoryName) || string.IsNullOrWhiteSpace(modelSourceRootPath) || !Directory.Exists(modelSourceRootPath)) {
                texturePath = string.Empty;
                return false;
            }

            string[] candidatePaths = Directory.GetFiles(modelSourceRootPath);
            for (int index = 0; index < candidatePaths.Length; index++) {
                string candidatePath = candidatePaths[index];
                if (!AssetImportManager.IsModelExtension(Path.GetExtension(candidatePath))) {
                    continue;
                }
                if (!string.Equals(Path.GetFileNameWithoutExtension(candidatePath), modelDirectoryName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (TryResolveTexturePathRelativeToDirectory(Path.GetDirectoryName(candidatePath), assetId, out texturePath)) {
                    return true;
                }
            }

            texturePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Attempts to resolve one texture identifier relative to the supplied source directory.
        /// </summary>
        /// <param name="baseDirectoryPath">Base directory that should receive the texture identifier.</param>
        /// <param name="assetId">Diffuse texture asset identifier stored on the material asset.</param>
        /// <param name="texturePath">Resolved absolute texture source path when available.</param>
        /// <returns>True when a matching texture exists; otherwise false.</returns>
        static bool TryResolveTexturePathRelativeToDirectory(string baseDirectoryPath, string assetId, out string texturePath) {
            if (string.IsNullOrWhiteSpace(baseDirectoryPath) || string.IsNullOrWhiteSpace(assetId)) {
                texturePath = string.Empty;
                return false;
            }

            string candidateTexturePath = Path.IsPathRooted(assetId)
                ? Path.GetFullPath(assetId)
                : Path.GetFullPath(Path.Combine(baseDirectoryPath, assetId));
            if (!File.Exists(candidateTexturePath)) {
                texturePath = string.Empty;
                return false;
            }

            texturePath = candidateTexturePath;
            return true;
        }

        /// <summary>
        /// Builds the packaged relative path used for one imported texture asset.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the material asset.</param>
        /// <returns>Cooked relative path under the build root.</returns>
        string BuildImportedTextureCookedRelativePath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            return NormalizeRelativePath(Path.Combine(ImportedTextureDirectoryName, assetId));
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
                try {
                    using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    AssetImportSettings settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                    if (settings.Processor?.Platforms != null && settings.Processor.Platforms.ContainsKey(TargetPlatformId)) {
                        return settings;
                    }
                } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(settingsPath, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Material settings file '{settingsPath}' could not be deserialized for material '{materialRelativePath}'.", ex);
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

            Dictionary<string, string> fieldValues = BuildMaterialCookFieldValues(materialAsset, platformMaterialSettings);
            return new PlatformMaterialCookRequest(
                materialAsset.Id ?? reference.RelativePath,
                reference.RelativePath,
                TargetPlatformId,
                SelectedBuildProfileId,
                SelectedGraphicsProfileId,
                platformMaterialSettings.SchemaId,
                fieldValues);
        }

        /// <summary>
        /// Builds the final field-value map used for one material cook request.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as a fallback source for authored values.</param>
        /// <param name="materialSettings">Target-platform material settings to translate.</param>
        /// <returns>Field-value map ready for builder consumption.</returns>
        Dictionary<string, string> BuildMaterialCookFieldValues(MaterialAsset materialAsset, MaterialAssetProcessorSettings materialSettings) {
            if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            Dictionary<string, string> fieldValues = materialSettings.FieldValues != null
                ? new Dictionary<string, string>(materialSettings.FieldValues, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool useCustomShader = IsCustomShaderEnabled(fieldValues);
            if (IsStandardShaderSchema(materialSettings.SchemaId) && !useCustomShader) {
                fieldValues[VariantFieldId] = StandardShaderVariantName;
                ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, StandardShaderFileName);
                fieldValues[ShaderAssetIdFieldId] = shaderAsset.Id;
                fieldValues[VertexProgramFieldId] = StandardVertexProgramName;
                fieldValues[PixelProgramFieldId] = StandardPixelProgramName;
            } else {
                fieldValues[VariantFieldId] = MeshVariantName;
            }

            if (IsStandardShaderSchema(materialSettings.SchemaId) && useCustomShader) {
                ResolveCustomShaderCookField(fieldValues, materialAsset, ShaderAssetIdFieldId, StandardShaderAssetId);
                ResolveCustomShaderCookField(fieldValues, materialAsset, VertexProgramFieldId, StandardVertexProgramName);
                ResolveCustomShaderCookField(fieldValues, materialAsset, PixelProgramFieldId, StandardPixelProgramName);
            }

            return fieldValues;
        }

        /// <summary>
        /// Preserves an authored custom shader field when present, otherwise falls back to the current material value or the supplied default.
        /// </summary>
        /// <param name="fieldValues">Field values being prepared for the cook request.</param>
        /// <param name="materialAsset">Current material asset used as the fallback source.</param>
        /// <param name="fieldId">Field identifier to resolve.</param>
        /// <param name="fallbackValue">Fallback value used when the material asset is blank.</param>
        void ResolveCustomShaderCookField(
            Dictionary<string, string> fieldValues,
            MaterialAsset materialAsset,
            string fieldId,
            string fallbackValue) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            string currentValue;
            if (fieldValues.TryGetValue(fieldId, out currentValue) && !string.IsNullOrWhiteSpace(currentValue)) {
                return;
            }

            string fallbackFieldValue = fieldId == ShaderAssetIdFieldId ? materialAsset.ShaderAssetId : fieldId == VertexProgramFieldId ? materialAsset.VertexProgram : materialAsset.PixelProgram;
            if (string.IsNullOrWhiteSpace(fallbackFieldValue)) {
                fallbackFieldValue = fallbackValue;
            }

            fieldValues[fieldId] = fallbackFieldValue ?? string.Empty;
        }

        /// <summary>
        /// Determines whether custom shader overrides are enabled in one field-value map.
        /// </summary>
        /// <param name="fieldValues">Material field values keyed by field id.</param>
        /// <returns>True when custom shader mode is enabled.</returns>
        bool IsCustomShaderEnabled(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                return false;
            }

            string customShaderValue;
            if (!fieldValues.TryGetValue(UseCustomShaderFieldId, out customShaderValue)) {
                return false;
            }

            return string.Equals(customShaderValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether one schema should use the standard shader defaults.
        /// </summary>
        /// <param name="schemaId">Material schema identifier to inspect.</param>
        /// <returns>True when the schema uses the standard shader path.</returns>
        bool IsStandardShaderSchema(string schemaId) {
            return string.Equals(schemaId, StandardShaderSchemaId, StringComparison.OrdinalIgnoreCase);
        }

        void EnsureGeneratedStandardMaterialAssets(string buildRootPath) {
            string shaderAssetId = StandardShaderAssetId;
            if (ShouldWriteGeneratedStandardShaderAsset()) {
                ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, StandardShaderFileName);
                shaderAssetId = shaderAsset.Id;
                WriteAsset(Path.Combine(buildRootPath, StandardGeneratedShaderRelativePath), shaderAsset);
            } else if (MaterialBuilder == null) {
                throw new InvalidOperationException("PS2 generated standard materials require a material builder.");
            }

            if (MaterialBuilder == null) {
                MaterialAsset materialAsset = new MaterialAsset {
                    Id = "Engine.Materials.Standard.material",
                    ShaderAssetId = shaderAssetId,
                    VertexProgram = StandardVertexProgramName,
                    PixelProgram = StandardPixelProgramName,
                    Variant = StandardShaderVariantName,
                    ConstantBuffers = [
                        StandardMaterialBaseColorDefaults.CreateWhiteConstantBufferAsset()
                    ]
                };
                WriteAsset(Path.Combine(buildRootPath, StandardGeneratedMaterialRelativePath), materialAsset);
                return;
            }

            MaterialAssetProcessorSettings standardMaterialSettings = new MaterialAssetProcessorSettings();
            MaterialAssetSchemaSettingsService schemaSettingsService = new MaterialAssetSchemaSettingsService();
            if (schemaSettingsService.EnsureSelectedSchema(standardMaterialSettings, MaterialBuilder.Definition.MaterialSchemas) == null) {
                throw new InvalidOperationException("The generated standard material requires at least one material schema.");
            }

            Dictionary<string, string> standardMaterialFieldValues = BuildMaterialCookFieldValues(new MaterialAsset(), standardMaterialSettings);
            standardMaterialFieldValues[VariantFieldId] = StandardShaderVariantName;
            PlatformMaterialCookResult cookResult = MaterialBuilder.CookMaterial(new PlatformMaterialCookRequest(
                StandardGeneratedMaterialAssetId,
                EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                TargetPlatformId,
                SelectedBuildProfileId,
                SelectedGraphicsProfileId,
                standardMaterialSettings.SchemaId,
                standardMaterialFieldValues));
            RememberReferencedShaderAssetIds(cookResult.ReferencedShaderAssetIds);
            WriteBytes(Path.Combine(buildRootPath, StandardGeneratedMaterialRelativePath), cookResult.CookedMaterialBytes);
        }

        /// <summary>
        /// Returns whether the generated standard shader asset should be written for the current target platform.
        /// </summary>
        /// <returns>True when the shared shader-backed standard material should be staged; otherwise false.</returns>
        bool ShouldWriteGeneratedStandardShaderAsset() {
            return !string.Equals(TargetPlatformId, "ps2", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Builds one cooked packaged-font relative path for an authored source-font reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative source-font path.</param>
        /// <returns>Cooked packaged-font relative path.</returns>
        string BuildCookedFontRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string changedExtensionPath = Path.ChangeExtension(normalizedRelativePath, ".hefont");
            return NormalizeRelativePath(Path.Combine("cooked", changedExtensionPath));
        }

        /// <summary>
        /// Builds one cooked packaged-material relative path for an authored file-backed material reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative material path.</param>
        /// <returns>Cooked packaged-material relative path.</returns>
        string BuildCookedMaterialRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return NormalizeRelativePath(Path.Combine("cooked", normalizedRelativePath));
        }

        /// <summary>
        /// Determines whether one material sidecar contains a usable schema for the requested platform.
        /// </summary>
        /// <param name="settings">Material sidecar settings to inspect.</param>
        /// <param name="platformId">Target platform id whose settings should be validated.</param>
        /// <returns>True when the sidecar contains a non-empty schema id for the requested platform.</returns>
        static bool HasValidPlatformMaterialSettings(AssetImportSettings settings, string platformId) {
            if (settings == null || string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }
            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return false;
            }
            if (!settings.Processor.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings)) {
                return false;
            }
            if (platformSettings == null || platformSettings.Material == null) {
                return false;
            }

            return !string.IsNullOrWhiteSpace(platformSettings.Material.SchemaId);
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

        /// <summary>
        /// Creates one serialized mesh component payload that references one project-relative material path.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string materialRelativePath) {
            if (string.IsNullOrWhiteSpace(materialRelativePath)) {
                throw new ArgumentException("Material relative path must be provided.", nameof(materialRelativePath));
            }

            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Material = new RuntimeMaterial()
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference("Material", new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = materialRelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            });

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveState);
            return record.Payload;
        }
    }
}
