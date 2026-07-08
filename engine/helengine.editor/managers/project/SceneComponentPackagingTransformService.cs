using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Paths;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Rewrites shared scene component payloads into packaged runtime forms.
    /// </summary>
    public sealed class SceneComponentPackagingTransformService {
        /// <summary>
        /// Stable serialized component id for mesh components.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";

        /// <summary>
        /// Stable serialized component id for camera components.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";

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
        /// Editor-side cache service that materializes platform-specific packaged font variants beneath the project cache root.
        /// </summary>
        readonly EditorPlatformFontVariantCacheService PlatformFontVariantCacheService;

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
        /// Platform-aware schema builder used to append builder-owned synthetic members to automatic runtime payloads.
        /// </summary>
        readonly PlatformExtendedScriptComponentSchemaBuilder PlatformExtendedSchemaBuilder;

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
        /// Reads detached component platform override payloads so packaging can resolve builder-owned synthetic member values.
        /// </summary>
        readonly ComponentPlatformOverridePayloadService PlatformOverridePayloadService;
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
        /// Registry that exposes the static mesh collision cook processor selected for this packaging run.
        /// </summary>
        readonly StaticMeshCollisionCookProcessorRegistry StaticMeshCookProcessorRegistry;

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
        /// <param name="staticMeshCookProcessorRegistry">Optional registry that exposes the active static mesh collision cook processor.</param>
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
            ITextComponentSpriteBakeService textComponentSpriteBakeService = null,
            StaticMeshCollisionCookProcessorRegistry staticMeshCookProcessorRegistry = null) {
            AssetsRootPath = string.IsNullOrWhiteSpace(assetsRootPath)
                ? throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath))
                : Path.GetFullPath(assetsRootPath);
            ProjectContentManager = projectContentManager ?? throw new ArgumentNullException(nameof(projectContentManager));
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            PlatformFontVariantCacheService = new EditorPlatformFontVariantCacheService(AssetImportManager);
            FileSystemModelResolver = fileSystemModelResolver ?? throw new ArgumentNullException(nameof(fileSystemModelResolver));
            FileSystemFontResolver = new EditorFileSystemFontResolver(AssetImportManager);
            FileSystemTextureResolver = new EditorFileSystemTextureResolver(AssetImportManager);
            ReferencedShaderAssetIds = referencedShaderAssetIds ?? throw new ArgumentNullException(nameof(referencedShaderAssetIds));
            ReferencedShaderAssetIdsSet = referencedShaderAssetIdsSet ?? throw new ArgumentNullException(nameof(referencedShaderAssetIdsSet));
            MaterialAssetSettingsService = new MaterialAssetSettingsService();
            string effectiveTargetPlatformId = targetPlatformId;
            if (string.IsNullOrWhiteSpace(effectiveTargetPlatformId) && !string.IsNullOrWhiteSpace(platformDefinition?.PlatformId)) {
                effectiveTargetPlatformId = platformDefinition.PlatformId;
            }
            if (string.IsNullOrWhiteSpace(effectiveTargetPlatformId)) {
                effectiveTargetPlatformId = string.Empty;
            }
            TargetPlatformId = effectiveTargetPlatformId;
            MaterialBuilder = materialBuilder;
            SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
            SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
            ScriptComponentSchemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            PlatformExtendedSchemaBuilder = new PlatformExtendedScriptComponentSchemaBuilder();
            ScriptTypeResolver = scriptTypeResolver;
            PlatformCookWorkItemSink = platformCookWorkItemSink;
            PlatformDefinition = platformDefinition;
            FileHasher = new AssetFileHasher();
            TextComponentSpriteBakeService = textComponentSpriteBakeService;
            StaticMeshCookProcessorRegistry = staticMeshCookProcessorRegistry ?? StaticMeshCollisionCookProcessorRegistry.Shared;
            AutomaticScriptComponentDescriptor = new AutomaticScriptComponentPersistenceDescriptor(ScriptComponentSchemaBuilder, scriptTypeResolver);
            PersistenceRegistry = new ComponentPersistenceRegistry(scriptTypeResolver);
            PlatformOverridePayloadService = new ComponentPlatformOverridePayloadService();
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

            if (string.Equals(componentTypeId, DirectionalLightComponentTypeId, StringComparison.OrdinalIgnoreCase)
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

            if (TryRewriteAutomaticComponentRecord(record, buildRootPath, out transformedRecord)) {
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

            EntitySaveComponent saveComponent = new EntitySaveComponent();
            SceneComponentAssetRecord baseRecord = PlatformOverridePayloadService.UnwrapBaseRecord(record);
            Component component = DeserializeAutomaticComponentForPackaging(baseRecord, descriptor, saveComponent);
            NormalizeAutomaticComponentForRuntimePackaging(component);
            ApplyStaticMeshCookedRuntimeData(component);
            transformedRecord = BuildAutomaticRuntimeComponentRecord(
                baseRecord.ComponentTypeId,
                baseRecord.ComponentIndex,
                component,
                ResolveAutomaticComponentSaveState(saveComponent, component),
                record,
                buildRootPath);
            return true;
        }

        /// <summary>
        /// Normalizes one automatic reflected component instance into the runtime layer space expected by packaged Windows players before it is serialized into the strict runtime payload.
        /// </summary>
        /// <param name="component">Live component instance under packaging.</param>
        void NormalizeAutomaticComponentForRuntimePackaging(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (component is CameraComponent cameraComponent
                && cameraComponent.LayerMask == EditorLayerMasks.SceneObjects) {
                cameraComponent.LayerMask = RuntimeSceneLayerMask;
            }
        }

        /// <summary>
        /// Applies one backend-owned static mesh runtime payload when the supplied component requires cook-time collision conversion.
        /// </summary>
        /// <param name="component">Live component instance under packaging.</param>
        void ApplyStaticMeshCookedRuntimeData(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            if (component is not StaticMeshCollider3DComponent staticMeshCollider) {
                return;
            } else if (staticMeshCollider.CollisionData == null) {
                throw new InvalidOperationException("Static mesh collider packaging requires collision data before backend cooking can run.");
            }

            IReadOnlyList<IStaticMeshCollisionCookProcessor3D> processors = StaticMeshCookProcessorRegistry.Processors;
            if (processors.Count == 0) {
                return;
            } else if (processors.Count > 1) {
                throw new InvalidOperationException("Static mesh collider packaging requires exactly one registered cook processor.");
            }

            IStaticMeshCollisionCookProcessor3D processor = processors[0];
            staticMeshCollider.CookedRuntimeData = StaticMeshCollisionRuntimeData3D.Create(
                processor.FormatId,
                processor.BinaryFormatId,
                processor.BinaryFormatVersion,
                ResolveAutomaticPayloadEndianness(),
                writer => processor.WritePayload(writer, staticMeshCollider.CollisionData));
        }

        /// <summary>
        /// Resolves the byte order used for nested engine-owned runtime payloads emitted during packaging.
        /// </summary>
        /// <returns>Resolved engine binary endianness.</returns>
        EngineBinaryEndianness ResolveAutomaticPayloadEndianness() {
            PlatformSerializationEndianness serializationEndianness = ResolvePlatformSerializationEndianness();
            if (serializationEndianness == PlatformSerializationEndianness.BigEndian) {
                return EngineBinaryEndianness.BigEndian;
            }

            return EngineBinaryEndianness.LittleEndian;
        }

        /// <summary>
        /// Resolves the target platform serialization endianness from the selected build and codegen profile metadata.
        /// </summary>
        /// <returns>Resolved platform serialization endianness.</returns>
        PlatformSerializationEndianness ResolvePlatformSerializationEndianness() {
            if (PlatformDefinition == null || PlatformDefinition.BuildProfiles.Length == 0 || PlatformDefinition.CodegenProfiles.Length == 0) {
                return PlatformSerializationEndianness.LittleEndian;
            }

            PlatformBuildProfileDefinition selectedBuildProfile = ResolveSelectedBuildProfile();
            if (selectedBuildProfile == null || string.IsNullOrWhiteSpace(selectedBuildProfile.CodegenProfileId)) {
                return PlatformSerializationEndianness.LittleEndian;
            }

            PlatformCodegenProfileDefinition selectedCodegenProfile = ResolveCodegenProfile(selectedBuildProfile.CodegenProfileId);
            return selectedCodegenProfile.Endianness;
        }

        /// <summary>
        /// Resolves the build profile currently selected for the packaging operation when one is available.
        /// </summary>
        /// <returns>Resolved build profile or null when no matching build profile is configured.</returns>
        PlatformBuildProfileDefinition ResolveSelectedBuildProfile() {
            if (PlatformDefinition == null || PlatformDefinition.BuildProfiles.Length == 0) {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(SelectedBuildProfileId)) {
                for (int index = 0; index < PlatformDefinition.BuildProfiles.Length; index++) {
                    PlatformBuildProfileDefinition buildProfile = PlatformDefinition.BuildProfiles[index];
                    if (string.Equals(buildProfile.ProfileId, SelectedBuildProfileId, StringComparison.OrdinalIgnoreCase)) {
                        return buildProfile;
                    }
                }
            }

            return PlatformDefinition.BuildProfiles[0];
        }

        /// <summary>
        /// Resolves one codegen profile by identifier from the target platform definition.
        /// </summary>
        /// <param name="codegenProfileId">Codegen profile identifier to resolve.</param>
        /// <returns>Resolved codegen profile.</returns>
        PlatformCodegenProfileDefinition ResolveCodegenProfile(string codegenProfileId) {
            if (string.IsNullOrWhiteSpace(codegenProfileId)) {
                throw new ArgumentException("Codegen profile id must be provided.", nameof(codegenProfileId));
            } else if (PlatformDefinition == null) {
                throw new InvalidOperationException("Platform definition is required before codegen profile endianness can be resolved.");
            }

            for (int index = 0; index < PlatformDefinition.CodegenProfiles.Length; index++) {
                PlatformCodegenProfileDefinition codegenProfile = PlatformDefinition.CodegenProfiles[index];
                if (string.Equals(codegenProfile.ProfileId, codegenProfileId, StringComparison.OrdinalIgnoreCase)) {
                    return codegenProfile;
                }
            }

            throw new InvalidOperationException($"Platform '{PlatformDefinition.PlatformId}' does not define codegen profile '{codegenProfileId}'.");
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
            SceneComponentAssetRecord sourceRecord,
            string buildRootPath) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (sourceRecord == null) {
                throw new ArgumentNullException(nameof(sourceRecord));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            ScriptComponentReflectionSchema schema = PlatformExtendedSchemaBuilder.Build(component.GetType(), PlatformDefinition);
            EntityComponentSaveState rewrittenSaveState = RewriteAutomaticComponentSaveStateReferences(schema, saveState, buildRootPath);
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                if (member.PlatformComponentMemberDefinition != null) {
                    WriteSyntheticPlatformMemberValue(writer, member, sourceRecord);
                    continue;
                }
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, rewrittenSaveState);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = componentTypeId,
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Writes one builder-owned synthetic platform member into the packaged runtime payload using the detached override value when authored or the platform default otherwise.
        /// </summary>
        /// <param name="writer">Destination writer receiving the ordinal runtime payload.</param>
        /// <param name="member">Synthetic schema member being serialized.</param>
        /// <param name="sourceRecord">Original authored scene component record that may contain detached platform overrides.</param>
        void WriteSyntheticPlatformMemberValue(
            EngineBinaryWriter writer,
            ScriptComponentReflectionMember member,
            SceneComponentAssetRecord sourceRecord) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (member == null) {
                throw new ArgumentNullException(nameof(member));
            }
            if (sourceRecord == null) {
                throw new ArgumentNullException(nameof(sourceRecord));
            }
            if (member.PlatformComponentMemberDefinition == null) {
                throw new InvalidOperationException("Synthetic platform member serialization requires a platform definition-backed schema member.");
            }

            string serializedValue = ResolveSyntheticPlatformMemberSerializedValue(sourceRecord, member.PlatformComponentMemberDefinition);
            object parsedValue = PlatformComponentMemberValueUtility.ParseValue(member.PlatformComponentMemberDefinition, serializedValue);
            AutomaticScriptComponentPersistenceDescriptor.WriteSupportedValue(writer, member.ValueType, parsedValue);
        }

        /// <summary>
        /// Resolves the detached serialized value authored for one builder-owned synthetic platform member or returns the platform default when no override exists.
        /// </summary>
        /// <param name="sourceRecord">Original authored scene component record that may contain detached platform overrides.</param>
        /// <param name="definition">Builder-owned synthetic platform member definition.</param>
        /// <returns>Serialized member value that should be emitted into the packaged runtime payload.</returns>
        string ResolveSyntheticPlatformMemberSerializedValue(
            SceneComponentAssetRecord sourceRecord,
            PlatformComponentMemberDefinition definition) {
            if (sourceRecord == null) {
                throw new ArgumentNullException(nameof(sourceRecord));
            }
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }
            if (PlatformDefinition == null || string.IsNullOrWhiteSpace(PlatformDefinition.PlatformId)) {
                return definition.DefaultValue;
            }

            IReadOnlyList<EntityComponentPlatformOverrideState> overrideStates = PlatformOverridePayloadService.ReadOverrideStates(sourceRecord);
            for (int index = 0; index < overrideStates.Count; index++) {
                EntityComponentPlatformOverrideState overrideState = overrideStates[index];
                if (!string.Equals(overrideState.PlatformId, PlatformDefinition.PlatformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (overrideState.TryGetMemberValue(definition.MemberName, out string overrideValue)) {
                    return overrideValue;
                }

                break;
            }

            return definition.DefaultValue;
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
                if (AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceType(member.ValueType)) {
                    if (!saveState.TryGetAssetReference(member.Name, out SceneAssetReference sourceReference)) {
                        continue;
                    }

                    rewrittenSaveState.SetAssetReference(
                        member.Name,
                        RewriteAutomaticComponentReference(member.ValueType, sourceReference, buildRootPath));
                    continue;
                }

                if (!AutomaticComponentAssetReferenceSupport.IsSupportedAssetReferenceArrayType(member.ValueType)) {
                    continue;
                }

                Type elementType = member.ValueType.GetElementType()
                    ?? throw new InvalidOperationException($"Automatic component asset-reference array '{member.Name}' must expose an element type.");
                string referenceNamePrefix = string.Concat(member.Name, "[");
                foreach (KeyValuePair<string, SceneAssetReference> namedReference in saveState.EnumerateNamedAssetReferences()) {
                    if (!namedReference.Key.StartsWith(referenceNamePrefix, StringComparison.Ordinal)) {
                        continue;
                    }

                    rewrittenSaveState.SetAssetReference(
                        namedReference.Key,
                        RewriteAutomaticComponentReference(elementType, namedReference.Value, buildRootPath));
                }
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
            if (valueType == typeof(AnimationClipAsset)) {
                return RewriteAnimationClipReference(reference, buildRootPath);
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
        /// Rewrites one authored animation-clip reference into the packaged file-backed reference consumed by runtime scripted component deserializers.
        /// </summary>
        /// <param name="reference">Serialized animation-clip reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed animation-clip reference.</returns>
        SceneAssetReference RewriteAnimationClipReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                throw new InvalidOperationException("AnimationClipAsset-backed component members require a clip reference before packaging.");
            }
            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                throw new InvalidOperationException($"Unsupported animation clip reference source kind '{reference.SourceKind}'.");
            }

            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            string cookedRelativePath = BuildCookedAnimationClipRelativePath(reference.RelativePath);
            CopyFile(sourcePath, Path.Combine(buildRootPath, cookedRelativePath));
            return CreateFileSystemReference(cookedRelativePath);
        }

        /// <summary>
        /// Builds the stable generated reference used for the editor's built-in font asset.
        /// </summary>
        /// <returns>Generated editor-font scene reference.</returns>
        SceneAssetReference BuildEditorFontReference() {
            return FontAssetScenePersistenceSupport.BuildEditorFontReference();
        }

        SceneAssetReference CreateFontFileReference(string relativePath) {
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemFont(ResolveRuntimeReferencePath(relativePath));
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
            if (SupportsBuilderOwnedFontAtlasCookKind()) {
                string cookedAtlasTextureRelativePath = BuildCookedFontAtlasTextureRelativePath(reference.RelativePath);
                if (string.Equals(Path.GetExtension(reference.RelativePath), ".hefont", StringComparison.OrdinalIgnoreCase)) {
                    FontAsset sourceFontAsset = LoadPackagedFontAssetForPackaging(sourcePath);
                    string generatedAtlasSourceFullPath = WriteGeneratedPackagedFontAtlasSource(buildRootPath, reference.RelativePath, sourceFontAsset);
                    FontAsset packagedFontAsset = PrepareFontAssetForExternalCookedAtlas(sourceFontAsset, cookedAtlasTextureRelativePath);
                    WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), packagedFontAsset);
                    string sourceAtlasAssetId = sourceFontAsset.SourceTextureAsset == null
                        ? string.Empty
                        : sourceFontAsset.SourceTextureAsset.Id ?? string.Empty;
                    RememberGeneratedFontCookWorkItem(generatedAtlasSourceFullPath, cookedAtlasTextureRelativePath, sourceAtlasAssetId);
                    return CreateFontFileReference(cookedRelativePath);
                }

                AssetImportSettings settings = AssetImportManager.LoadOrCreateImportSettings(sourcePath);
                EditorPlatformFontVariantCacheResult fontVariant = PlatformFontVariantCacheService.ResolveVariant(sourcePath, TargetPlatformId);
                FontAsset cachedFontAsset = LoadPackagedFontAssetForPackaging(fontVariant.CachedFontAssetPath);
                FontAsset cachedPackagedFontAsset = PrepareFontAssetForExternalCookedAtlas(cachedFontAsset, cookedAtlasTextureRelativePath);
                WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), cachedPackagedFontAsset);
                RememberFontCookWorkItem(fontVariant.CachedAtlasTextureAssetPath, cookedAtlasTextureRelativePath, settings);
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
        /// Writes one generated texture asset for a packaged font that still embeds raw atlas bytes so builder-owned texture cooking can consume only generic texture assets.
        /// </summary>
        /// <param name="buildRootPath">Absolute build root path that receives the generated texture asset.</param>
        /// <param name="relativePath">Project-relative packaged font path whose atlas is being externalized.</param>
        /// <param name="fontAsset">Packaged font asset that still owns one embedded source atlas texture.</param>
        /// <returns>Absolute generated texture asset path written under the build root.</returns>
        string WriteGeneratedPackagedFontAtlasSource(string buildRootPath, string relativePath, FontAsset fontAsset) {
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            } else if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            } else if (fontAsset.SourceTextureAsset == null) {
                throw new InvalidOperationException("Packaged source fonts must include one embedded atlas texture before builder-owned cooking can externalize it.");
            }

            string normalizedRelativePath = NormalizeSourceRelativePath(relativePath);
            string generatedRelativePath = Path.Combine("generated", "packaged-font-atlases", Path.ChangeExtension(normalizedRelativePath, ".hasset") ?? throw new InvalidOperationException("Generated packaged font atlas path could not be resolved."));
            string generatedAtlasSourceFullPath = Path.Combine(buildRootPath, generatedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            WriteAsset(generatedAtlasSourceFullPath, fontAsset.SourceTextureAsset);
            return generatedAtlasSourceFullPath;
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
                CopyReferencedRoughnessTextureAsset(fullPath, ResolveReferencedRoughnessTextureAssetId(materialAsset, cookRequest.FieldValues), buildRootPath);
                WriteBytes(Path.Combine(buildRootPath, cookedRelativePath), cookResult.CookedMaterialBytes);
                return CreateFileSystemReference(cookedRelativePath);
            }

            RememberReferencedShaderAssetId(materialAsset.ShaderAssetId);
            CopyReferencedDiffuseTextureAsset(fullPath, materialAsset, buildRootPath);
            CopyReferencedRoughnessTextureAsset(fullPath, materialAsset, buildRootPath);

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
        /// Copies one imported roughness texture asset referenced by a material into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose roughness texture should be packaged.</param>
        /// <param name="materialAsset">Material asset whose imported roughness texture should be packaged.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedRoughnessTextureAsset(string materialAssetPath, ShaderMaterialAsset materialAsset, string buildRootPath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            CopyReferencedTextureAsset(materialAssetPath, materialAsset.RoughnessTextureAssetId, buildRootPath);
        }

        /// <summary>
        /// Copies one imported diffuse texture asset referenced by a cooked material request into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose diffuse texture should be packaged.</param>
        /// <param name="diffuseTextureAssetId">Imported diffuse texture asset id that should be copied.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedDiffuseTextureAsset(string materialAssetPath, string diffuseTextureAssetId, string buildRootPath) {
            CopyReferencedTextureAsset(materialAssetPath, diffuseTextureAssetId, buildRootPath);
        }

        /// <summary>
        /// Copies one imported roughness texture asset referenced by a cooked material request into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose roughness texture should be packaged.</param>
        /// <param name="roughnessTextureAssetId">Imported roughness texture asset id that should be copied.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedRoughnessTextureAsset(string materialAssetPath, string roughnessTextureAssetId, string buildRootPath) {
            CopyReferencedTextureAsset(materialAssetPath, roughnessTextureAssetId, buildRootPath);
        }

        /// <summary>
        /// Copies one imported texture asset referenced by a material into the packaged content root.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material asset whose texture should be packaged.</param>
        /// <param name="textureAssetId">Imported texture asset id that should be copied.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void CopyReferencedTextureAsset(string materialAssetPath, string textureAssetId, string buildRootPath) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }
            if (string.IsNullOrWhiteSpace(textureAssetId)) {
                return;
            }

            TextureAsset textureAsset;
            if (!AssetImportManager.TryLoadImportedTextureAsset(textureAssetId, out textureAsset) || textureAsset == null) {
                string sourcePath = ResolveImportedTextureAssetPath(textureAssetId);
                throw new InvalidOperationException($"Imported texture '{textureAssetId}' at '{sourcePath}' could not be loaded for packaging.");
            }
            string cookedRelativePath = BuildImportedTextureCookedRelativePath(textureAssetId);
            if (!SupportsBuilderOwnedPlatformCookKind("texture")) {
                WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), textureAsset);
            }
            if (!SupportsBuilderOwnedPlatformCookKind("texture")) {
                return;
            }

            string importedTextureSourcePath;
            try {
                importedTextureSourcePath = ResolveImportedTextureSourcePath(textureAssetId);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Imported texture '{textureAssetId}' referenced by material '{materialAssetPath}' could not resolve an authored source texture path.", ex);
            }
            TextureAssetImportSettings settings;
            if (!AssetImportManager.TryLoadOrCreateTextureImportSettings(importedTextureSourcePath, out settings) || settings == null) {
                throw new InvalidOperationException($"Imported texture '{textureAssetId}' from source '{importedTextureSourcePath}' could not create import settings for builder-owned platform cooking.");
            }
            RememberTextureCookWorkItem(NormalizeRelativePath(Path.GetRelativePath(AssetsRootPath, importedTextureSourcePath)), importedTextureSourcePath, cookedRelativePath, settings);
        }

        /// <summary>
        /// Resolves the diffuse texture asset id that one builder-backed material request will require at runtime.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as the fallback source.</param>
        /// <param name="fieldValues">Final builder field values prepared for cooking.</param>
        /// <returns>Imported diffuse texture asset id that should be copied, or an empty string when the material has no imported texture.</returns>
        string ResolveReferencedDiffuseTextureAssetId(ShaderMaterialAsset materialAsset, IReadOnlyDictionary<string, string> fieldValues) {
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
                !string.IsNullOrWhiteSpace(textureRelativePath)) {
                if (!string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId) &&
                    ImportedTextureRuntimePathResolver.PathMatchesAssetId(TargetPlatformId, textureRelativePath, materialAsset.DiffuseTextureAssetId)) {
                    return materialAsset.DiffuseTextureAssetId;
                }
                if (TryResolveImportedTextureAssetIdFromCookedRelativePath(textureRelativePath, out string cookedTextureAssetId)) {
                return cookedTextureAssetId;
                }
            }

            return materialAsset.DiffuseTextureAssetId ?? string.Empty;
        }

        /// <summary>
        /// Resolves the roughness texture asset id that one builder-backed material request will require at runtime.
        /// </summary>
        /// <param name="materialAsset">Source material asset used as the fallback source.</param>
        /// <param name="fieldValues">Final builder field values prepared for cooking.</param>
        /// <returns>Imported roughness texture asset id that should be copied, or an empty string when the material has no imported roughness texture.</returns>
        static string ResolveReferencedRoughnessTextureAssetId(ShaderMaterialAsset materialAsset, IReadOnlyDictionary<string, string> fieldValues) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            if (fieldValues != null &&
                fieldValues.TryGetValue("roughness-texture-id", out string roughnessTextureAssetId) &&
                !string.IsNullOrWhiteSpace(roughnessTextureAssetId)) {
                return roughnessTextureAssetId;
            }

            return materialAsset.RoughnessTextureAssetId ?? string.Empty;
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
            if (candidateAssetId.EndsWith(".hetex", StringComparison.OrdinalIgnoreCase)) {
                candidateAssetId = candidateAssetId.Substring(0, candidateAssetId.Length - ".hetex".Length);
            }
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

            return ImportedTextureRuntimePathResolver.BuildCookedRelativePath(TargetPlatformId, assetId);
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

        void RememberFontCookWorkItem(string sourceAssetPath, string cookedRelativePath, AssetImportSettings settings) {
            if (PlatformCookWorkItemSink == null || !SupportsBuilderOwnedFontAtlasCookKind()) {
                return;
            } else if (string.IsNullOrWhiteSpace(sourceAssetPath)) {
                throw new ArgumentException("Source asset path must be provided.", nameof(sourceAssetPath));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateGeneratedFontAtlasTextureWorkItem(
                PlatformDefinition,
                TargetPlatformId,
                sourceAssetPath,
                cookedRelativePath,
                string.Empty,
                settings,
                FileHasher);
            if (workItem != null) {
                PlatformCookWorkItemSink(workItem);
            }
        }

        /// <summary>
        /// Records one builder-owned generated font-atlas cook work item for a packaged font atlas written under the build root.
        /// </summary>
        /// <param name="sourceAssetPath">Absolute generated texture asset path that the builder should cook.</param>
        /// <param name="cookedRelativePath">Runtime-relative cooked atlas texture path the builder must produce.</param>
        /// <param name="sourceAssetId">Stable identifier of the generated source texture asset, or an empty string when the output path should become the fallback identifier.</param>
        void RememberGeneratedFontCookWorkItem(string sourceAssetPath, string cookedRelativePath, string sourceAssetId) {
            if (PlatformCookWorkItemSink == null || !SupportsBuilderOwnedFontAtlasCookKind()) {
                return;
            } else if (string.IsNullOrWhiteSpace(sourceAssetPath)) {
                throw new ArgumentException("Source asset path must be provided.", nameof(sourceAssetPath));
            } else if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path must be provided.", nameof(cookedRelativePath));
            }

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateGeneratedFontAtlasTextureWorkItem(
                PlatformDefinition,
                TargetPlatformId,
                sourceAssetPath,
                cookedRelativePath,
                sourceAssetId,
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
        /// Returns whether the selected platform publishes one builder-owned font-atlas cook capability or a generic texture fallback for font atlases.
        /// </summary>
        /// <returns>True when the builder should own cooked font-atlas generation for the selected platform.</returns>
        bool SupportsBuilderOwnedFontAtlasCookKind() {
            return ResolveBuilderOwnedFontAtlasCookCapability() != null;
        }

        /// <summary>
        /// Resolves the builder-owned cook capability used for externalized packaged font atlases.
        /// </summary>
        /// <returns>Dedicated font-atlas capability when published; otherwise a generic builder-owned texture capability; otherwise null.</returns>
        PlatformAssetCookCapabilityDefinition ResolveBuilderOwnedFontAtlasCookCapability() {
            return ResolveBuilderOwnedPlatformCookCapability("font-atlas-texture")
                ?? ResolveBuilderOwnedPlatformCookCapability("texture");
        }

        /// <summary>
        /// Resolves one builder-owned cook capability by source asset kind.
        /// </summary>
        /// <param name="sourceAssetKind">Generic source asset kind to probe.</param>
        /// <returns>Resolved builder-owned cook capability, or null when the platform does not publish one.</returns>
        PlatformAssetCookCapabilityDefinition ResolveBuilderOwnedPlatformCookCapability(string sourceAssetKind) {
            if (PlatformDefinition == null) {
                return null;
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
                    return capability;
                }
            }

            return null;
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
                MaterialAssetImportSettings materialSettings = MaterialAssetSettingsService.LoadOrCreateInMemory(
                    materialAssetPath,
                    materialAsset,
                    [TargetPlatformId],
                    ResolveSelectionModelForMaterialSettings);
                return materialSettings;
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
            return global::helengine.SceneAssetReferenceFactory.Rehydrate(
                SceneAssetReferenceSourceKind.FileSystem,
                ResolveRuntimeReferencePath(relativePath),
                string.Empty,
                string.Empty);
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

            return global::helengine.SceneAssetReferenceFactory.Rehydrate(
                SceneAssetReferenceSourceKind.Generated,
                ResolveRuntimeReferencePath(relativePath),
                providerId,
                assetId);
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
            return normalizedRelativePath;
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
        /// Builds one cooked relative path for an authored animation clip so the final build graph stages the clip beside other runtime assets.
        /// </summary>
        /// <param name="relativePath">Original project-relative animation clip path.</param>
        /// <returns>Cooked packaged animation-clip relative path.</returns>
        string BuildCookedAnimationClipRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return NormalizeRelativePath(Path.Combine("cooked", normalizedRelativePath));
        }

        /// <summary>
        /// Builds one cooked atlas-texture relative path for an authored source-font reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative source-font path.</param>
        /// <returns>Cooked packaged atlas-texture relative path.</returns>
        string BuildCookedFontAtlasTextureRelativePath(string relativePath) {
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            PlatformAssetCookCapabilityDefinition capability = ResolveBuilderOwnedFontAtlasCookCapability();
            string outputExtension = capability == null || string.IsNullOrWhiteSpace(capability.OutputFileExtension)
                ? ".hetex"
                : capability.OutputFileExtension;
            string changedExtensionPath = Path.ChangeExtension(normalizedRelativePath, outputExtension);
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
            return global::helengine.SceneAssetReferenceFactory.ReadOptionalReference(reader);
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

    }
}

