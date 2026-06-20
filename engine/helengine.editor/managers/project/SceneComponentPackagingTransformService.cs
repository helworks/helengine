using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Paths;
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
        /// Stable serialized component id for directional light components.
        /// </summary>
        const string DirectionalLightComponentTypeId = "helengine.DirectionalLightComponent";

        /// <summary>
        /// Stable serialized component id for ambient light components.
        /// </summary>
        const string AmbientLightComponentTypeId = "helengine.AmbientLightComponent";

        /// <summary>
        /// Stable serialized component id for point light components.
        /// </summary>
        const string PointLightComponentTypeId = "helengine.PointLightComponent";

        /// <summary>
        /// Stable serialized component id for spot light components.
        /// </summary>
        const string SpotLightComponentTypeId = "helengine.SpotLightComponent";

        /// <summary>
        /// Stable serialized component id for 3D rigid-body components.
        /// </summary>
        const string RigidBody3DComponentTypeId = "helengine.RigidBody3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D box-collider components.
        /// </summary>
        const string BoxCollider3DComponentTypeId = "helengine.BoxCollider3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D kinematic-motion components.
        /// </summary>
        const string KinematicMotion3DComponentTypeId = "helengine.KinematicMotion3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D character-controller components.
        /// </summary>
        const string CharacterController3DComponentTypeId = "helengine.CharacterController3DComponent";

        /// <summary>
        /// Serialized byte length of the legacy rigid-body payload that predates packaged angular velocity.
        /// </summary>
        const int LegacyRigidBodyPayloadLength = 23;

        /// <summary>
        /// Serialized byte length of the current strict rigid-body payload.
        /// </summary>
        const int CurrentRigidBodyPayloadLength = 35;

        /// <summary>
        /// Serialized byte length of the legacy box-collider payload that only stores size.
        /// </summary>
        const int LegacyBoxColliderPayloadLength = 13;

        /// <summary>
        /// Serialized byte length of the current strict box-collider payload.
        /// </summary>
        const int CurrentBoxColliderPayloadLength = 18;

        /// <summary>
        /// Serialized byte length of the strict kinematic-motion payload.
        /// </summary>
        const int KinematicMotionPayloadLength = 34;

        /// <summary>
        /// Serialized byte length of the strict character-controller payload.
        /// </summary>
        const int CharacterControllerPayloadLength = 45;

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
        const string CubeGeneratedAssetId = EngineGeneratedModelCache.CubeAssetId;

        /// <summary>
        /// Stable generated model asset id for the built-in plane primitive.
        /// </summary>
        const string PlaneGeneratedAssetId = EngineGeneratedModelCache.PlaneAssetId;

        /// <summary>
        /// Stable generated model asset id for the built-in sphere primitive.
        /// </summary>
        const string SphereGeneratedAssetId = EngineGeneratedModelCache.SphereAssetId;

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
        /// Resolver used to obtain imported `FontAsset` payloads for file-backed source fonts.
        /// </summary>
        readonly EditorFileSystemFontResolver FileSystemFontResolver;
        /// <summary>
        /// Resolver used to obtain imported `TextureAsset` payloads for file-backed source textures.
        /// </summary>
        readonly EditorFileSystemTextureResolver FileSystemTextureResolver;
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
        /// Target platform id whose material settings should drive packaged mirrored material payloads.
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
        /// Callback that records builder-owned platform cook work items discovered while rewriting scene content.
        /// </summary>
        readonly Action<PlatformCookWorkItem> PlatformCookWorkItemSink;
        /// <summary>
        /// Platform definition that publishes builder-owned asset cook capabilities for the current packaging target.
        /// </summary>
        readonly PlatformDefinition PlatformDefinition;
        /// <summary>
        /// Hasher used to compute stable source and settings hashes for platform cook work items.
        /// </summary>
        readonly AssetFileHasher FileHasher;

        /// <summary>
        /// Optional bake service used to convert authored text into generated sprite textures during packaging.
        /// </summary>
        readonly ITextComponentSpriteBakeService TextComponentSpriteBakeService;

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
        /// <param name="platformCookWorkItemSink">Optional callback that records builder-owned platform cook work items discovered while packaging.</param>
        /// <param name="platformDefinition">Optional platform definition that publishes builder-owned asset cook capabilities.</param>
        /// <param name="textComponentSpriteBakeService">Optional bake service used to convert authored text components into sprite-backed runtime payloads.</param>
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
            IScriptTypeResolver scriptTypeResolver = null,
            Action<PlatformCookWorkItem> platformCookWorkItemSink = null,
            PlatformDefinition platformDefinition = null,
            ITextComponentSpriteBakeService textComponentSpriteBakeService = null) {
            AssetsRootPath = string.IsNullOrWhiteSpace(assetsRootPath)
                ? throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath))
                : Path.GetFullPath(assetsRootPath);
            ProjectContentManager = projectContentManager ?? throw new ArgumentNullException(nameof(projectContentManager));
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            FileSystemModelResolver = fileSystemModelResolver ?? throw new ArgumentNullException(nameof(fileSystemModelResolver));
            FileSystemFontResolver = new EditorFileSystemFontResolver(AssetImportManager);
            FileSystemTextureResolver = new EditorFileSystemTextureResolver(AssetImportManager);
            ReferencedShaderAssetIds = referencedShaderAssetIds ?? throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            ReferencedShaderAssetIdsSet = referencedShaderAssetIdsSet ?? throw new ArgumentNullException(nameof(referencedShaderAssetIdsSet));
            MaterialAssetSettingsService = new MaterialAssetSettingsService();
            TargetPlatformId = string.IsNullOrWhiteSpace(targetPlatformId) ? "windows" : targetPlatformId;
            MaterialBuilder = materialBuilder;
            SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
            SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
            ScriptComponentSchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptTypeResolver = scriptTypeResolver;
            PlatformCookWorkItemSink = platformCookWorkItemSink;
            PlatformDefinition = platformDefinition;
            FileHasher = new AssetFileHasher();
            TextComponentSpriteBakeService = textComponentSpriteBakeService;
            AutomaticScriptComponentDescriptor = new AutomaticScriptComponentPersistenceDescriptor(ScriptComponentSchemaBuilder, scriptTypeResolver);
            CameraComponentDescriptor = new CameraComponentPersistenceDescriptor();
            PersistenceRegistry = new ComponentPersistenceRegistry(scriptTypeResolver);
            PersistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            PersistenceRegistry.Register(CameraComponentDescriptor);
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
                || string.Equals(componentTypeId, DirectionalLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, AmbientLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, PointLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, SpotLightComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
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

            if (UsesLegacyBuiltInPhysicsPayload(record) &&
                TryRewriteBuiltInPhysicsComponentRecord(record, buildRootPath, out transformedRecord)) {
                return true;
            }

            if (TryRewriteAutomaticComponentRecord(record, buildRootPath, out transformedRecord)) {
                return true;
            }

            if (TryRewriteBuiltInPhysicsComponentRecord(record, buildRootPath, out transformedRecord)) {
                return true;
            }

            transformedRecord = null;
            return false;
        }

        /// <summary>
        /// Rewrites one built-in physics component record into the shared automatic runtime payload shape expected by default packaged-scene loading.
        /// </summary>
        /// <param name="record">Serialized physics component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <param name="transformedRecord">Rewritten automatic runtime component record when the component type is supported.</param>
        /// <returns>True when the component record was rewritten as a built-in physics payload; otherwise false.</returns>
        bool TryRewriteBuiltInPhysicsComponentRecord(
            SceneComponentAssetRecord record,
            string buildRootPath,
            out SceneComponentAssetRecord transformedRecord) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (!string.Equals(record.ComponentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.ComponentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.ComponentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(record.ComponentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                transformedRecord = null;
                return false;
            }

            Component component = DeserializeBuiltInPhysicsComponentForPackaging(record);
            transformedRecord = BuildAutomaticRuntimeComponentRecord(
                record.ComponentTypeId,
                record.ComponentIndex,
                component,
                null,
                buildRootPath);
            return true;
        }

        /// <summary>
        /// Deserializes one built-in physics component record through the matching runtime deserializer so legacy strict payloads can be normalized during packaging.
        /// </summary>
        /// <param name="record">Serialized physics component record to materialize.</param>
        /// <returns>Live physics component instance reconstructed from the serialized payload.</returns>
        Component DeserializeBuiltInPhysicsComponentForPackaging(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.Equals(record.ComponentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return new RuntimeRigidBody3DComponentDeserializer().Deserialize(record, null);
            }
            if (string.Equals(record.ComponentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return new RuntimeBoxCollider3DComponentDeserializer().Deserialize(record, null);
            }
            if (string.Equals(record.ComponentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return new RuntimeKinematicMotion3DComponentDeserializer().Deserialize(record, null);
            }
            if (string.Equals(record.ComponentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return new RuntimeCharacterController3DComponentDeserializer().Deserialize(record, null);
            }

            throw new InvalidOperationException($"Built-in physics packaging does not support component type '{record.ComponentTypeId}'.");
        }

        /// <summary>
        /// Returns whether one built-in physics component payload uses a legacy strict binary layout that must bypass the generic automatic reflected path.
        /// </summary>
        /// <param name="record">Serialized component record to inspect.</param>
        /// <returns>True when the payload uses a strict built-in physics layout; otherwise false.</returns>
        bool UsesLegacyBuiltInPhysicsPayload(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            if (payload.Length == 0) {
                return false;
            }

            if (string.Equals(record.ComponentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return (payload[0] == 1 && payload.Length == LegacyRigidBodyPayloadLength)
                    || (payload[0] == 2 && payload.Length == CurrentRigidBodyPayloadLength);
            }
            if (string.Equals(record.ComponentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return (payload[0] == 1 && payload.Length == LegacyBoxColliderPayloadLength)
                    || (payload[0] == 2 && payload.Length == CurrentBoxColliderPayloadLength);
            }
            if (string.Equals(record.ComponentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return payload[0] == 1 && payload.Length == KinematicMotionPayloadLength;
            }
            if (string.Equals(record.ComponentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
                return payload[0] == 1 && payload.Length == CharacterControllerPayloadLength;
            }

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
        /// Rewrites one named editor reflected-component payload into the strict packaged ordinal payload shape.
        /// </summary>
        /// <param name="record">Serialized component record to rewrite.</param>
        /// <param name="transformedRecord">Rewritten component record when successful.</param>
        /// <returns>True when the record was rewritten through the automatic reflected fallback; otherwise false.</returns>
        bool TryRewriteAutomaticComponentRecord(
            SceneComponentAssetRecord record,
            string buildRootPath,
            out SceneComponentAssetRecord transformedRecord) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (!TryResolvePersistenceDescriptor(record.ComponentTypeId, out IComponentPersistenceDescriptor descriptor)) {
                transformedRecord = null;
                return false;
            }

            if (descriptor is not AutomaticScriptComponentPersistenceDescriptor) {
                transformedRecord = null;
                return false;
            }

            try {
                EntitySaveComponent saveComponent = new EntitySaveComponent();
                Component component = DeserializeAutomaticComponentForPackaging(record, descriptor, saveComponent);
                transformedRecord = BuildAutomaticRuntimeComponentRecord(
                    record.ComponentTypeId,
                    record.ComponentIndex,
                    component,
                    ResolveAutomaticComponentSaveState(saveComponent, component),
                    buildRootPath);
                return true;
            } catch (InvalidOperationException) when (CanPreserveCurrentRuntimePayload(record.ComponentTypeId)) {
                transformedRecord = null;
                return false;
            }
        }

        /// <summary>
        /// Returns whether the supplied component type already uses a runtime-safe binary payload shape that packaging should preserve when automatic reflected deserialization does not apply.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id to inspect.</param>
        /// <returns>True when packaging should keep the existing runtime payload bytes unchanged.</returns>
        bool CanPreserveCurrentRuntimePayload(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return false;
            }

            return string.Equals(componentTypeId, RigidBody3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, BoxCollider3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, KinematicMotion3DComponentTypeId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(componentTypeId, CharacterController3DComponentTypeId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds one strict runtime automatic-component payload from the supplied live component instance.
        /// </summary>
        /// <param name="componentTypeId">Serialized runtime component type id to emit.</param>
        /// <param name="componentIndex">Stable component index assigned within the owning entity.</param>
        /// <param name="component">Live component instance whose reflected members should be serialized.</param>
        /// <returns>Runtime-ready automatic component record.</returns>
        SceneComponentAssetRecord BuildAutomaticRuntimeComponentRecord(
            string componentTypeId,
            int componentIndex,
            Component component,
            EntityComponentSaveState saveState,
            string buildRootPath) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            ScriptComponentReflectionSchema schema = ScriptComponentSchemaBuilder.Build(component.GetType());
            EntityComponentSaveState rewrittenSaveState = RewriteAutomaticComponentSaveStateReferences(schema, saveState, buildRootPath);
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, rewrittenSaveState);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = componentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Deserializes one automatic scripted component for packaging through its resolved persistence descriptor.
        /// </summary>
        /// <param name="record">Serialized component record being packaged.</param>
        /// <param name="descriptor">Resolved persistence descriptor for the component type.</param>
        /// <returns>Live component instance ready for runtime payload emission.</returns>
        Component DeserializeAutomaticComponentForPackaging(
            SceneComponentAssetRecord record,
            IComponentPersistenceDescriptor descriptor,
            EntitySaveComponent saveComponent) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            return descriptor.DeserializeComponent(record, saveComponent, null);
        }

        /// <summary>
        /// Rewrites the saved asset references used by one automatic reflected component into packaged runtime references.
        /// </summary>
        /// <param name="schema">Reflected component schema whose asset-backed members should be inspected.</param>
        /// <param name="saveState">Editor-time asset reference state restored for the component.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Cloned save-state containing packaged runtime references when asset references exist; otherwise the original save-state.</returns>
        EntityComponentSaveState RewriteAutomaticComponentSaveStateReferences(
            ScriptComponentReflectionSchema schema,
            EntityComponentSaveState saveState,
            string buildRootPath) {
            if (schema == null) {
                throw new ArgumentNullException(nameof(schema));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (saveState == null) {
                return null;
            }

            EntityComponentSaveState rewrittenSaveState = new EntityComponentSaveState {
                ComponentKey = saveState.ComponentKey
            };

            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                if (!AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
                    continue;
                }
                if (!saveState.TryGetAssetReference(member.Name, out SceneAssetReference sourceReference)) {
                    continue;
                }

                rewrittenSaveState.SetAssetReference(
                    member.Name,
                    RewriteAutomaticComponentReference(member.ValueType, sourceReference, buildRootPath));
            }

            return rewrittenSaveState;
        }

        /// <summary>
        /// Rewrites one automatic reflected asset reference according to the runtime asset type exposed by the persisted member.
        /// </summary>
        /// <param name="valueType">Runtime asset type expected by the persisted member.</param>
        /// <param name="reference">Editor-time source reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged runtime asset reference.</returns>
        SceneAssetReference RewriteAutomaticComponentReference(Type valueType, SceneAssetReference reference, string buildRootPath) {
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (valueType == typeof(FontAsset)) {
                return RewriteFontReference(reference, buildRootPath);
            }
            if (valueType == typeof(RuntimeTexture)) {
                return RewriteTextureReference(reference, buildRootPath);
            }
            if (valueType == typeof(RuntimeModel)) {
                return RewriteModelReference(reference, buildRootPath);
            }
            if (valueType == typeof(RuntimeMaterial)) {
                return RewriteMaterialReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Automatic component reference rewriting does not support asset type '{valueType.FullName}'.");
        }

        /// <summary>
        /// Resolves the temporary component save-state materialized while one automatic reflected component is prepared for packaged runtime rewriting.
        /// </summary>
        /// <param name="saveComponent">Temporary save-component that collected any restored asset references.</param>
        /// <param name="component">Automatic reflected component whose save-state should be resolved.</param>
        /// <returns>Resolved component save-state when one exists; otherwise null.</returns>
        static EntityComponentSaveState ResolveAutomaticComponentSaveState(EntitySaveComponent saveComponent, Component component) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (saveComponent.TryGetComponentState(component, out EntityComponentSaveState saveState)) {
                return saveState;
            }

            return null;
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

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            if (IsLegacyVersionedMeshPayload(payload)) {
                throw new InvalidOperationException($"Unsupported mesh component payload version '{ReadPayloadVersion(payload)}'.");
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

            byte[] payload = record.Payload ?? Array.Empty<byte>();
            if (IsLegacyVersionedCameraPayload(payload)) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{ReadPayloadVersion(payload)}'.");
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
        /// Returns whether one camera payload uses the legacy versioned binary layout instead of the tagged editor field format.
        /// </summary>
        /// <param name="payload">Serialized camera payload bytes to inspect.</param>
        /// <returns>True when the payload should be interpreted as the legacy camera binary format.</returns>
        bool IsLegacyVersionedCameraPayload(byte[] payload) {
            if (payload == null || payload.Length == 0) {
                return false;
            }

            byte version = payload[0];
            if (version != 1 && version != 2 && version != CameraComponentPayloadVersion) {
                return false;
            }

            if (version != EditorTaggedSceneComponentPayloadFormat.CurrentVersion) {
                return true;
            }

            if (payload.Length < 5) {
                return true;
            }

            int fieldCount = BitConverter.ToInt32(payload, 1);
            if (fieldCount < 0) {
                return true;
            }

            return fieldCount > payload.Length;
        }

        /// <summary>
        /// Returns whether one mesh payload uses an older versioned binary layout instead of the tagged editor field format.
        /// </summary>
        /// <param name="payload">Serialized mesh payload bytes to inspect.</param>
        /// <returns>True when the payload should be rejected as an older binary format.</returns>
        bool IsLegacyVersionedMeshPayload(byte[] payload) {
            if (payload == null || payload.Length == 0) {
                return false;
            }

            byte version = payload[0];
            if (version != EditorTaggedSceneComponentPayloadFormat.CurrentVersion) {
                return true;
            }

            if (payload.Length < 5) {
                return true;
            }

            int fieldCount = BitConverter.ToInt32(payload, 1);
            if (fieldCount < 0) {
                return true;
            }

            return fieldCount > payload.Length;
        }

        /// <summary>
        /// Returns whether one payload uses the strict ordinal automatic runtime-component layout with the expected reflected member count.
        /// </summary>
        /// <param name="payload">Serialized payload bytes to inspect.</param>
        /// <param name="expectedMemberCount">Expected reflected member count for the automatic runtime component.</param>
        /// <returns>True when the payload matches the ordinal automatic runtime-component layout; otherwise false.</returns>
        bool IsAutomaticRuntimePayloadWithExpectedMemberCount(byte[] payload, int expectedMemberCount) {
            if (payload == null || payload.Length < 5) {
                return false;
            }

            int memberCount = BitConverter.ToInt32(payload, 1);
            return memberCount == expectedMemberCount;
        }

        /// <summary>
        /// Reads the leading payload version byte when one is available.
        /// </summary>
        /// <param name="payload">Serialized payload bytes to inspect.</param>
        /// <returns>Leading payload version byte or zero when the payload is empty.</returns>
        static byte ReadPayloadVersion(byte[] payload) {
            if (payload == null || payload.Length == 0) {
                return 0;
            }

            return payload[0];
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
        /// Reads one serialized automatic-component asset reference restored through the hidden save-state component.
        /// </summary>
        /// <param name="saveComponent">Hidden save-state component populated while deserializing the automatic payload.</param>
        /// <param name="component">Live component instance whose asset reference should be resolved.</param>
        /// <param name="memberName">Public member name that owns the asset reference.</param>
        /// <returns>Persisted scene asset reference when one exists; otherwise null.</returns>
        SceneAssetReference ReadAutomaticComponentAssetReference(EntitySaveComponent saveComponent, Component component, string memberName) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Member name must be provided.", nameof(memberName));
            }

            if (!saveComponent.TryGetComponentState(component, out EntityComponentSaveState saveState)) {
                return null;
            }

            string referenceName = AutomaticComponentAssetReferenceSupport.BuildReferenceName(memberName);
            if (!saveState.TryGetAssetReference(referenceName, out SceneAssetReference reference)) {
                return null;
            }

            return reference;
        }
        /// <summary>
        /// Rewrites one authored font reference into the packaged font reference consumed by strict runtime component payloads.
        /// </summary>
        /// <param name="reference">Authored font reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged font reference.</returns>
        SceneAssetReference RewriteFontReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                throw new InvalidOperationException("FPSComponent requires a font reference before packaging.");
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                if (!string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}'.");
                }
                if (string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    return CreateFontFileReference(EditorFontRelativePath);
                }
                throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}'.");
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return RewriteFileSystemFontReference(reference, buildRootPath);
            }

            throw new InvalidOperationException($"Unsupported font reference source kind '{reference.SourceKind}'.");
        }

        /// <summary>
        /// Rewrites one texture reference into a cooked packaged texture reference.
        /// </summary>
        /// <param name="reference">Serialized texture reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed texture reference.</returns>
        SceneAssetReference RewriteTextureReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                throw new InvalidOperationException("SpriteComponent requires a texture reference before packaging.");
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                throw new InvalidOperationException($"Unsupported texture reference source kind '{reference.SourceKind}'.");
            }

            return RewriteFileSystemTextureReference(reference, buildRootPath);
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
                RelativePath = ResolveRuntimeReferencePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Rewrites one file-backed source texture reference into a cooked packaged texture reference.
        /// </summary>
        /// <param name="reference">Serialized texture reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed texture reference.</returns>
        SceneAssetReference RewriteFileSystemTextureReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            TextureAssetImportSettings settings;
            if (!AssetImportManager.TryLoadOrCreateTextureImportSettings(sourcePath, out settings) || settings == null) {
                throw new InvalidOperationException($"Texture source '{reference.RelativePath}' could not create import settings for packaging.");
            }
            if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                throw new InvalidOperationException($"Texture source '{reference.RelativePath}' did not produce an imported asset id for packaging.");
            }
            if (!AssetImportManager.TryLoadTextureAsset(sourcePath, out TextureAsset textureAsset) || textureAsset == null) {
                throw new InvalidOperationException($"Texture source '{reference.RelativePath}' could not be imported for packaging.");
            }

            string cookedRelativePath = BuildImportedTextureCookedRelativePath(settings.Importer.AssetId);
            if (!SupportsBuilderOwnedPlatformCookKind("texture")) {
                WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), textureAsset);
            }
            RememberTextureCookWorkItem(reference.RelativePath, sourcePath, cookedRelativePath, settings);
            return CreateFileSystemReference(cookedRelativePath);
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
            if (SupportsBuilderOwnedPlatformCookKind("font-atlas-texture")) {
                string cookedAtlasTextureRelativePath = BuildCookedFontAtlasTextureRelativePath(reference.RelativePath);
                FontAsset sourceFontAsset;
                if (string.Equals(Path.GetExtension(reference.RelativePath), ".hefont", StringComparison.OrdinalIgnoreCase)) {
                    sourceFontAsset = LoadPackagedFontAssetForPackaging(sourcePath);
                } else {
                    sourceFontAsset = LoadImportedFontAssetForPackaging(reference, sourcePath);
                }

                FontAsset packagedFontAsset = PrepareFontAssetForExternalCookedAtlas(sourceFontAsset, cookedAtlasTextureRelativePath);
                WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), packagedFontAsset);
                RememberFontCookWorkItem(reference.RelativePath, sourcePath, cookedAtlasTextureRelativePath);
                return CreateFontFileReference(cookedRelativePath);
            }

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

        /// <summary>
        /// Loads one packaged source font asset from disk so the packaging step can rewrite it for one platform-owned atlas path.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the packaged source font.</param>
        /// <returns>Loaded packaged font asset.</returns>
        FontAsset LoadPackagedFontAssetForPackaging(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return helengine.files.FontAssetBinarySerializer.Deserialize(stream);
        }

        /// <summary>
        /// Creates one packaged font asset clone that resolves its atlas through one external cooked texture path instead of embedded raw atlas bytes.
        /// </summary>
        /// <param name="fontAsset">Source font asset that still carries the raw atlas payload.</param>
        /// <param name="cookedAtlasTextureRelativePath">Logical cooked atlas texture path that the packaged font should reference at runtime.</param>
        /// <returns>Packaged font asset rewritten for one external cooked atlas texture.</returns>
        FontAsset PrepareFontAssetForExternalCookedAtlas(FontAsset fontAsset, string cookedAtlasTextureRelativePath) {
            if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            } else if (fontAsset.FontInfo == null) {
                throw new InvalidOperationException("Packaged fonts must include font metrics before atlas externalization.");
            } else if (string.IsNullOrWhiteSpace(cookedAtlasTextureRelativePath)) {
                throw new ArgumentException("Cooked atlas texture relative path must be provided.", nameof(cookedAtlasTextureRelativePath));
            }

            Dictionary<char, FontChar> characters = fontAsset.Characters == null
                ? new Dictionary<char, FontChar>()
                : new Dictionary<char, FontChar>(fontAsset.Characters);

            return new FontAsset(
                new FontInfo(fontAsset.FontInfo.Name, fontAsset.FontInfo.LineSpacing, fontAsset.FontInfo.SpaceWidth),
                null,
                characters,
                fontAsset.LineHeight,
                fontAsset.AtlasWidth,
                fontAsset.AtlasHeight) {
                    SourceTextureAsset = null,
                CookedAtlasTextureRelativePath = ResolveRuntimeReferencePath(cookedAtlasTextureRelativePath)
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
                return CreateGeneratedPackagedReference(
                    BuildRuntimeModelReferenceRelativePath(relativePath),
                    reference.ProviderId,
                    reference.AssetId);
            }

            if (string.Equals(reference.AssetId, PlaneGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "cooked/engine/models/plane.hasset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One));
                return CreateGeneratedPackagedReference(
                    BuildRuntimeModelReferenceRelativePath(relativePath),
                    reference.ProviderId,
                    reference.AssetId);
            }

            if (string.Equals(reference.AssetId, SphereGeneratedAssetId, StringComparison.Ordinal)) {
                string relativePath = "cooked/engine/models/sphere.hasset";
                WriteAsset(Path.Combine(buildRootPath, relativePath), ModelUtils.GenerateSphereMesh(float3.Zero, float3.One));
                return CreateGeneratedPackagedReference(
                    BuildRuntimeModelReferenceRelativePath(relativePath),
                    reference.ProviderId,
                    reference.AssetId);
            }

            throw new InvalidOperationException($"Unsupported generated model asset id '{reference.AssetId}'.");
        }

        SceneAssetReference RewriteFileSystemModelReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            ModelAsset modelAsset = FileSystemModelResolver.ResolveModelAsset(sourcePath);
            string relativePath = BuildImportedModelRelativePath(reference.RelativePath);
            WriteAsset(Path.Combine(buildRootPath, relativePath), modelAsset);
            return CreateFileSystemReference(BuildRuntimeModelReferenceRelativePath(relativePath));
        }

        SceneAssetReference RewriteGeneratedMaterialReference(SceneAssetReference reference, string buildRootPath) {
            if (!string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material provider '{reference.ProviderId}'.");
            }
            if (!string.Equals(reference.AssetId, StandardGeneratedMaterialAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated material asset id '{reference.AssetId}'.");
            }

            EnsureGeneratedStandardMaterialAssets(buildRootPath);
            return CreateGeneratedPackagedReference(StandardGeneratedMaterialRelativePath, reference.ProviderId, reference.AssetId);
        }

        SceneAssetReference RewriteFileSystemMaterialReference(SceneAssetReference reference, string buildRootPath) {
            string fullPath = ResolveProjectAssetPath(reference.RelativePath);
            ShaderMaterialAsset materialAsset;
            try {
                materialAsset = MaterialAssetSettingsService.LoadMaterialAsset(fullPath, TargetPlatformId);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' at '{fullPath}' could not be loaded for packaging.", ex);
            }
            string cookedRelativePath = BuildCookedMaterialRelativePath(reference.RelativePath);
            if (MaterialBuilder != null) {
                MaterialAssetImportSettings materialSettings = LoadMaterialSettingsForCook(fullPath, reference.RelativePath, materialAsset);
                PlatformMaterialCookRequest cookRequest = BuildMaterialCookRequest(reference, materialAsset, materialSettings);
                PlatformMaterialCookResult cookResult = MaterialBuilder.CookMaterial(cookRequest);
                RememberReferencedShaderAssetIds(cookResult.ReferencedShaderAssetIds);

                CopyReferencedDiffuseTextureAsset(fullPath, ResolveReferencedDiffuseTextureAssetId(materialAsset, cookRequest.FieldValues), buildRootPath);
                WriteBytes(Path.Combine(buildRootPath, cookedRelativePath), cookResult.CookedMaterialBytes);
                return CreateFileSystemReference(cookedRelativePath);
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
        void CopyReferencedDiffuseTextureAsset(string materialAssetPath, ShaderMaterialAsset materialAsset, string buildRootPath) {
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
            if (!AssetImportManager.TryLoadImportedTextureAsset(diffuseTextureAssetId, out textureAsset) || textureAsset == null) {
                string sourcePath = ResolveImportedTextureAssetPath(diffuseTextureAssetId);
                throw new InvalidOperationException($"Imported texture '{diffuseTextureAssetId}' at '{sourcePath}' could not be loaded for packaging.");
            }
            string cookedRelativePath = BuildImportedTextureCookedRelativePath(diffuseTextureAssetId);
            if (!SupportsBuilderOwnedPlatformCookKind("texture")) {
                WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), textureAsset);
            }
            if (!SupportsBuilderOwnedPlatformCookKind("texture")) {
                return;
            }

            string importedTexturePath = ResolveImportedTextureAssetPath(diffuseTextureAssetId);
            string importedTextureSourcePath;
            try {
                importedTextureSourcePath = ResolveImportedTextureSourcePath(diffuseTextureAssetId);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Imported texture '{diffuseTextureAssetId}' referenced by material '{materialAssetPath}' could not resolve an authored source texture path.", ex);
            }
            TextureAssetImportSettings settings;
            if (!AssetImportManager.TryLoadOrCreateTextureImportSettings(importedTextureSourcePath, out settings) || settings == null) {
                throw new InvalidOperationException($"Imported texture '{diffuseTextureAssetId}' from source '{importedTextureSourcePath}' could not create import settings for builder-owned platform cooking.");
            }
            RememberTextureCookWorkItem(NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, importedTextureSourcePath)), importedTextureSourcePath, cookedRelativePath, settings);
        }

        /// <summary>
        /// Resolves the diffuse texture asset id that one builder-backed material request will require at runtime.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as the fallback source.</param>
        /// <param name="fieldValues">Final builder field values prepared for cooking.</param>
        /// <returns>Imported diffuse texture asset id that should be copied, or an empty string when the material has no imported texture.</returns>
        static string ResolveReferencedDiffuseTextureAssetId(ShaderMaterialAsset materialAsset, IReadOnlyDictionary<string, string> fieldValues) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (fieldValues != null &&
                fieldValues.TryGetValue("texture-id", out string diffuseTextureAssetId) &&
                !string.IsNullOrWhiteSpace(diffuseTextureAssetId)) {
                return diffuseTextureAssetId;
            }

            if (fieldValues != null &&
                fieldValues.TryGetValue("texture-relative-path", out string textureRelativePath) &&
                TryResolveImportedTextureAssetIdFromCookedRelativePath(textureRelativePath, out string cookedTextureAssetId)) {
                return cookedTextureAssetId;
            }

            return materialAsset.DiffuseTextureAssetId ?? string.Empty;
        }

        /// <summary>
        /// Extracts one imported texture asset id from a cooked runtime texture path saved by platform material settings.
        /// </summary>
        /// <param name="textureRelativePath">Runtime texture path stored in material cook fields.</param>
        /// <param name="assetId">Resolved imported texture asset id when the path points at the imported texture directory.</param>
        /// <returns>True when the cooked path references one imported texture cache asset.</returns>
        static bool TryResolveImportedTextureAssetIdFromCookedRelativePath(string textureRelativePath, out string assetId) {
            assetId = string.Empty;
            if (string.IsNullOrWhiteSpace(textureRelativePath)) {
                return false;
            }

            string normalizedPath = textureRelativePath.Replace('\\', '/');
            string importedTexturePrefix = ImportedTextureDirectoryName + "/";
            if (!normalizedPath.StartsWith(importedTexturePrefix, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string candidateAssetId = normalizedPath.Substring(importedTexturePrefix.Length);
            if (string.IsNullOrWhiteSpace(candidateAssetId) || candidateAssetId.Contains('/')) {
                return false;
            }

            assetId = candidateAssetId;
            return true;
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
        /// Resolves one imported texture asset id back to the authored source texture file that produced it.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the material asset.</param>
        /// <returns>Absolute path to the authored source texture file.</returns>
        string ResolveImportedTextureSourcePath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string sourcePath;
            if (!AssetImportManager.TryResolveImportedTextureSourcePath(assetId, out sourcePath) || string.IsNullOrWhiteSpace(sourcePath)) {
                throw new InvalidOperationException($"Imported texture '{assetId}' could not be resolved back to one authored source texture path.");
            }

            return sourcePath;
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

        void RememberTextureCookWorkItem(
            string sourceRelativePath,
            string sourcePath,
            string cookedRelativePath,
            TextureAssetImportSettings settings) {
            if (PlatformCookWorkItemSink == null || !SupportsBuilderOwnedPlatformCookKind("texture")) {
                return;
            } else if (string.IsNullOrWhiteSpace(sourceRelativePath)) {
                throw new ArgumentException("Source relative path must be provided.", nameof(sourceRelativePath));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateTextureWorkItem(
                PlatformDefinition,
                TargetPlatformId,
                Path.GetDirectoryName(AssetsRootPath) ?? throw new InvalidOperationException("Assets root parent path could not be resolved."),
                NormalizeSourceRelativePath(sourceRelativePath),
                cookedRelativePath,
                settings,
                FileHasher);
            if (workItem != null) {
                PlatformCookWorkItemSink(workItem);
            }
        }

        /// <summary>
        /// Records one builder-owned cook work item for a generated text-sprite texture written during scene packaging.
        /// </summary>
        /// <param name="sourceAssetPath">Absolute generated texture source path written under the build root.</param>
        /// <param name="cookedRelativePath">Runtime-relative cooked output path that the builder should produce.</param>
        /// <param name="bakeResult">Generated text-sprite bake result containing the source asset id and texture settings.</param>
        void RememberGeneratedTextureCookWorkItem(
            string sourceAssetPath,
            string cookedRelativePath,
            TextComponentSpriteBakeResult bakeResult) {
            if (PlatformCookWorkItemSink == null || !SupportsBuilderOwnedPlatformCookKind("texture")) {
                return;
            } else if (string.IsNullOrWhiteSpace(sourceAssetPath)) {
                throw new ArgumentException("Source asset path must be provided.", nameof(sourceAssetPath));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            } else if (bakeResult == null) {
                throw new ArgumentNullException(nameof(bakeResult));
            }

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateGeneratedTextureWorkItem(
                PlatformDefinition,
                TargetPlatformId,
                sourceAssetPath,
                cookedRelativePath,
                bakeResult.TextureAsset?.Id,
                bakeResult.ProcessorSettings,
                FileHasher);
            if (workItem != null) {
                PlatformCookWorkItemSink(workItem);
            }
        }

        void RememberFontCookWorkItem(string sourceRelativePath, string sourcePath, string cookedRelativePath) {
            if (PlatformCookWorkItemSink == null || !SupportsBuilderOwnedPlatformCookKind("font-atlas-texture")) {
                return;
            } else if (string.IsNullOrWhiteSpace(sourceRelativePath)) {
                throw new ArgumentException("Source relative path must be provided.", nameof(sourceRelativePath));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            AssetImportSettings settings;
            if (!AssetImportManager.TryLoadOrCreateImportSettings(sourcePath, out settings) || settings == null) {
                throw new InvalidOperationException($"Font source '{sourceRelativePath}' could not create import settings for builder-owned platform cooking.");
            }

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateFontAtlasTextureWorkItem(
                PlatformDefinition,
                TargetPlatformId,
                Path.GetDirectoryName(AssetsRootPath) ?? throw new InvalidOperationException("Assets root parent path could not be resolved."),
                NormalizeSourceRelativePath(sourceRelativePath),
                cookedRelativePath,
                settings,
                FileHasher);
            if (workItem != null) {
                PlatformCookWorkItemSink(workItem);
            }
        }

        /// <summary>
        /// Returns whether the selected platform publishes one builder-owned cook capability for the supplied source asset kind.
        /// </summary>
        /// <param name="sourceAssetKind">Generic source asset kind to probe.</param>
        /// <returns>True when the selected platform wants the builder to own this asset-kind cook step.</returns>
        bool SupportsBuilderOwnedPlatformCookKind(string sourceAssetKind) {
            if (PlatformDefinition == null) {
                return false;
            } else if (string.IsNullOrWhiteSpace(sourceAssetKind)) {
                throw new ArgumentException("Source asset kind must be provided.", nameof(sourceAssetKind));
            }

            PlatformAssetCookCapabilityDefinition[] capabilities = PlatformDefinition.AssetCookCapabilities ?? [];
            for (int index = 0; index < capabilities.Length; index++) {
                PlatformAssetCookCapabilityDefinition capability = capabilities[index];
                if (capability == null) {
                    continue;
                }
                if (!string.Equals(capability.SourceAssetKind, sourceAssetKind, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (capability.OwnershipKind == PlatformAssetCookOwnershipKind.BuilderOwned) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Loads one material-settings sidecar for build-time material translation.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset.</param>
        /// <param name="materialRelativePath">Project-relative material asset path used in diagnostics.</param>
        /// <returns>Deserialized material settings sidecar.</returns>
        MaterialAssetImportSettings LoadMaterialSettingsForCook(string materialAssetPath, string materialRelativePath, ShaderMaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (string.IsNullOrWhiteSpace(materialRelativePath)) {
                throw new ArgumentException("Material relative path must be provided.", nameof(materialRelativePath));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
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
            ShaderMaterialAsset materialAsset,
            MaterialAssetImportSettings materialSettings) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            }

            MaterialAssetProcessorSettings platformMaterialSettings = materialSettings.Processor.Platforms[TargetPlatformId];
            if (platformMaterialSettings == null) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' is missing material settings for target platform '{TargetPlatformId}'.");
            } else if (string.IsNullOrWhiteSpace(platformMaterialSettings.SchemaId)) {
                throw new InvalidOperationException($"Material '{reference.RelativePath}' is missing a schema id for target platform '{TargetPlatformId}'.");
            }

            MaterialAssetProcessorSettings cookMaterialSettings = ResolveCookMaterialSettings(platformMaterialSettings);
            Dictionary<string, string> fieldValues = BuildMaterialCookFieldValues(materialAsset, cookMaterialSettings);
            string materialAssetId = string.IsNullOrWhiteSpace(materialAsset.Id)
                ? reference.RelativePath
                : materialAsset.Id;
            return new PlatformMaterialCookRequest(
                materialAssetId,
                reference.RelativePath,
                TargetPlatformId,
                SelectedBuildProfileId,
                SelectedGraphicsProfileId,
                cookMaterialSettings.SchemaId,
                fieldValues);
        }

        /// <summary>
        /// Resolves the effective material settings used for one builder cook request without mutating the authored asset import document.
        /// </summary>
        /// <param name="platformMaterialSettings">Authored per-platform material settings.</param>
        /// <returns>Cook-request material settings compatible with the active builder contract.</returns>
        MaterialAssetProcessorSettings ResolveCookMaterialSettings(MaterialAssetProcessorSettings platformMaterialSettings) {
            if (platformMaterialSettings == null) {
                throw new ArgumentNullException(nameof(platformMaterialSettings));
            } else if (!UsesCookedPlatformOwnedMaterialResolution() || !IsStandardShaderSchema(platformMaterialSettings.SchemaId)) {
                return platformMaterialSettings;
            }

            MaterialAssetProcessorSettings cookMaterialSettings = CloneMaterialSettings(platformMaterialSettings);
            MaterialAssetSchemaSettingsService schemaSettingsService = new MaterialAssetSchemaSettingsService();
            if (schemaSettingsService.EnsureSelectedSchema(cookMaterialSettings, MaterialBuilder.Definition.MaterialSchemas) == null) {
                throw new InvalidOperationException("Cooked-platform-owned builders require at least one material schema.");
            }

            return cookMaterialSettings;
        }

        /// <summary>
        /// Clones one per-platform material settings object so build-time normalization does not rewrite authored import metadata in place.
        /// </summary>
        /// <param name="platformMaterialSettings">Per-platform material settings to copy.</param>
        /// <returns>Independent copy of the supplied material settings.</returns>
        MaterialAssetProcessorSettings CloneMaterialSettings(MaterialAssetProcessorSettings platformMaterialSettings) {
            if (platformMaterialSettings == null) {
                throw new ArgumentNullException(nameof(platformMaterialSettings));
            }

            return new MaterialAssetProcessorSettings {
                SchemaId = platformMaterialSettings.SchemaId,
                FieldValues = platformMaterialSettings.FieldValues != null
                    ? new Dictionary<string, string>(platformMaterialSettings.FieldValues, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Builds the final field-value map used for one material cook request.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as a fallback source for authored values.</param>
        /// <param name="materialSettings">Target-platform material settings to translate.</param>
        /// <returns>Field-value map ready for builder consumption.</returns>
        Dictionary<string, string> BuildMaterialCookFieldValues(ShaderMaterialAsset materialAsset, MaterialAssetProcessorSettings materialSettings) {
            if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            Dictionary<string, string> fieldValues = materialSettings.FieldValues != null
                ? new Dictionary<string, string>(materialSettings.FieldValues, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool useCustomShader = IsCustomShaderEnabled(fieldValues);
            bool usesCookedPlatformOwnedMaterialResolution = UsesCookedPlatformOwnedMaterialResolution();
            if (IsStandardShaderSchema(materialSettings.SchemaId) && !useCustomShader) {
                fieldValues[VariantFieldId] = StandardShaderVariantName;
                if (!usesCookedPlatformOwnedMaterialResolution) {
                    ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, StandardShaderFileName);
                    fieldValues[ShaderAssetIdFieldId] = shaderAsset.Id;
                    fieldValues[VertexProgramFieldId] = StandardVertexProgramName;
                    fieldValues[PixelProgramFieldId] = StandardPixelProgramName;
                }
            } else {
                fieldValues[VariantFieldId] = MeshVariantName;
            }

            if (IsStandardShaderSchema(materialSettings.SchemaId) && useCustomShader) {
                ResolveCustomShaderCookField(fieldValues, materialAsset, ShaderAssetIdFieldId, StandardShaderAssetId);
                ResolveCustomShaderCookField(fieldValues, materialAsset, VertexProgramFieldId, StandardVertexProgramName);
                ResolveCustomShaderCookField(fieldValues, materialAsset, PixelProgramFieldId, StandardPixelProgramName);
            }

            ApplyImportedTextureCookField(fieldValues, materialAsset);

            return fieldValues;
        }

        /// <summary>
        /// Populates one builder-visible imported texture path when the target material schema expects a cooked runtime texture payload.
        /// </summary>
        /// <param name="fieldValues">Cook field-value map being prepared for the platform builder.</param>
        /// <param name="materialAsset">Source material asset used as the fallback source for authored values.</param>
        void ApplyImportedTextureCookField(Dictionary<string, string> fieldValues, ShaderMaterialAsset materialAsset) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (fieldValues.TryGetValue("texture-relative-path", out string textureRelativePath) &&
                !string.IsNullOrWhiteSpace(textureRelativePath)) {
                return;
            }

            string diffuseTextureAssetId = ResolveReferencedDiffuseTextureAssetId(materialAsset, fieldValues);
            if (string.IsNullOrWhiteSpace(diffuseTextureAssetId)) {
                return;
            }

            fieldValues["texture-relative-path"] = BuildImportedTextureCookedRelativePath(diffuseTextureAssetId);
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
            ShaderMaterialAsset materialAsset,
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

        /// <summary>
        /// Returns whether the active material builder resolves materials through the cooked-platform-owned runtime contract.
        /// </summary>
        /// <returns>True when the active material builder uses cooked-platform-owned material resolution.</returns>
        bool UsesCookedPlatformOwnedMaterialResolution() {
            if (MaterialBuilder == null || MaterialBuilder.Definition == null || MaterialBuilder.Definition.RuntimeGenerationContract == null) {
                return false;
            }

            return MaterialBuilder.Definition.RuntimeGenerationContract.MaterialResolutionMode == RuntimeMaterialResolutionMode.CookedPlatformOwned;
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
                ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
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

            Dictionary<string, string> standardMaterialFieldValues = BuildMaterialCookFieldValues(new ShaderMaterialAsset(), standardMaterialSettings);
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
            return !UsesCookedPlatformOwnedMaterialResolution();
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
                RelativePath = ResolveRuntimeReferencePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Creates one packaged generated asset reference that preserves generated ownership semantics while pointing at one cooked file.
        /// </summary>
        /// <param name="relativePath">Cooked packaged file path.</param>
        /// <param name="providerId">Stable generated provider id.</param>
        /// <param name="assetId">Stable generated asset id.</param>
        /// <returns>Generated scene asset reference targeting the cooked packaged file.</returns>
        SceneAssetReference CreateGeneratedPackagedReference(string relativePath, string providerId, string assetId) {
            if (string.IsNullOrWhiteSpace(providerId)) {
                throw new ArgumentException("Generated provider id must be provided.", nameof(providerId));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Generated asset id must be provided.", nameof(assetId));
            }

            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = ResolveRuntimeReferencePath(relativePath),
                ProviderId = providerId,
                AssetId = assetId
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
        /// Resolves one packaged model asset path into the runtime artifact path consumed by the selected platform.
        /// </summary>
        /// <param name="relativePath">Packaged model asset path written into the build root.</param>
        /// <returns>Runtime model asset path that should be serialized into scene references.</returns>
        string BuildRuntimeModelReferenceRelativePath(string relativePath) {
            string normalizedRelativePath = NormalizeRelativePath(relativePath);
            if (!string.Equals(TargetPlatformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
                return normalizedRelativePath;
            }

            return NormalizeRelativePath(Path.ChangeExtension(normalizedRelativePath, ".phm"));
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
        /// Builds one cooked PS2 atlas-texture relative path for an authored source-font reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative source-font path.</param>
        /// <returns>Cooked packaged atlas-texture relative path.</returns>
        string BuildCookedFontAtlasTextureRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string changedExtensionPath = Path.ChangeExtension(normalizedRelativePath, ".ps2tex");
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
        static bool HasValidPlatformMaterialSettings(MaterialAssetProcessorSettings platformSettings) {
            if (platformSettings == null) {
                return false;
            }

            return !string.IsNullOrWhiteSpace(platformSettings.SchemaId);
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

            return CanonicalPackagedAssetPath.Normalize(relativePath);
        }

        /// <summary>
        /// Normalizes one project-relative source path to a stable slash direction without rewriting authored casing.
        /// </summary>
        /// <param name="relativePath">Project-relative source path to normalize.</param>
        /// <returns>Project-relative path that uses forward slashes.</returns>
        string NormalizeSourceRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Resolves one logical packaged asset path into the final runtime path form required by the selected platform contract.
        /// </summary>
        /// <param name="relativePath">Logical packaged asset path relative to the build content root.</param>
        /// <returns>Final runtime asset path consumed by the selected platform.</returns>
        string ResolveRuntimeReferencePath(string relativePath) {
            string normalizedRelativePath = CanonicalPackagedAssetPath.ValidateCanonical(relativePath);
            if (PlatformDefinition == null || PlatformDefinition.RuntimeGenerationContract == null) {
                return normalizedRelativePath;
            }

            return PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
                TargetPlatformId,
                PlatformDefinition.RuntimeGenerationContract,
                normalizedRelativePath);
        }

        /// <summary>
        /// Resolves the absolute project root path that owns the supplied assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute project assets root path.</param>
        /// <returns>Absolute project root path.</returns>
        static string ResolveProjectRootPath(string assetsRootPath) {
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }

            DirectoryInfo assetsDirectory = Directory.GetParent(Path.GetFullPath(assetsRootPath));
            if (assetsDirectory == null) {
                throw new InvalidOperationException("Project root path could not be resolved from the assets root.");
            }

            return assetsDirectory.FullName;
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

