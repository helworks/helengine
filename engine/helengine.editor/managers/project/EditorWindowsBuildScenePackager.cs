using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor {
    /// <summary>
    /// Packages selected editor scenes and their required runtime assets into one Windows player content root.
    /// </summary>
    public sealed class EditorPlatformBuildScenePackager {
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
        /// Stable serialized component id for 3D rigid-body components.
        /// </summary>
        const string RigidBody3DComponentTypeId = "helengine.RigidBody3DComponent";

        /// <summary>
        /// Stable serialized component id for 3D box collider components.
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
        /// Runtime scene layer used by the current Windows player loader for materialized entities.
        /// </summary>
        const ushort RuntimeSceneLayerMask = 0b00000001;

        /// <summary>
        /// Stable generated-asset provider id used by engine-generated scene references.
        /// </summary>
        const string EngineGeneratedProviderId = "engine";

        /// <summary>
        /// Generated provider id reserved for the editor's built-in font asset.
        /// </summary>
        const string EditorGeneratedProviderId = "editor";

        /// <summary>
        /// Stable asset id used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontAssetId = "ui-font";

        /// <summary>
        /// Packaged relative path used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontRelativePath = "cooked/fonts/default.hefont";

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
        /// Stable shader asset identifier used by the packaged standard material.
        /// </summary>
        const string StandardShaderAssetId = "ForwardStandardShader";

        /// <summary>
        /// Vertex program name used by the packaged generated standard material.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";

        /// <summary>
        /// Pixel program name used by the packaged generated standard material.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";

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
        /// Absolute project root that owns the source `assets` folder.
        /// </summary>
        readonly string ProjectRootPath;

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
        /// Resolver used to obtain processed `ModelAsset` payloads for file-backed source models.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;

        /// <summary>
        /// Deduplicated shader asset ids referenced while packaging the current scene set.
        /// </summary>
        readonly List<string> ReferencedShaderAssetIds;

        /// <summary>
        /// Importer registrations supplied by the editor host for source-backed asset loading.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;

        /// <summary>
        /// Fast lookup used to deduplicate referenced shader asset ids while preserving discovery order.
        /// </summary>
        readonly HashSet<string> ReferencedShaderAssetIdsSet;

        /// <summary>
        /// Builder-provided component compatibility metadata keyed by serialized type id.
        /// </summary>
        readonly Dictionary<string, PlatformComponentCompatibilityDefinition> ComponentCompatibilitiesByTypeId;

        /// <summary>
        /// Platform identifier used for diagnostics.
        /// </summary>
        readonly string PlatformId;

        /// <summary>
        /// Shared transform service used when platform compatibility marks a component as transformable.
        /// </summary>
        readonly SceneComponentPackagingTransformService TransformService;

        /// <summary>
        /// Packaged editor font asset used when scenes reference the built-in editor font.
        /// </summary>
        readonly FontAsset DefaultFontAsset;

        /// <summary>
        /// Initializes one Windows scene packager for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath)
            : this(projectRootPath, Array.Empty<IAssetImporterRegistration>(), (PlatformDefinition)null, null) {
        }

        /// <summary>
        /// Initializes one Windows scene packager for the supplied project root and importer registrations.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers)
            : this(projectRootPath, importers, (PlatformDefinition)null, null) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, and default font asset.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="defaultFontAsset">Packaged default font asset used by player builds.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers, FontAsset defaultFontAsset)
            : this(projectRootPath, importers, (PlatformDefinition)null, defaultFontAsset) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, and target platform id.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="targetPlatformId">Platform id that should be reported to the asset-import pipeline.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers, string targetPlatformId)
            : this(projectRootPath, importers, targetPlatformId, (PlatformDefinition)null, null, null) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, and platform definition.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDefinition">Builder-provided platform definition that carries compatibility metadata.</param>
        public EditorPlatformBuildScenePackager(string projectRootPath, IReadOnlyList<IAssetImporterRegistration> importers, PlatformDefinition platformDefinition)
            : this(projectRootPath, importers, platformDefinition?.PlatformId, platformDefinition, null, null) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, platform definition, and default font asset.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDefinition">Builder-provided platform definition that carries compatibility metadata.</param>
        /// <param name="defaultFontAsset">Packaged default font asset used by player builds.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        public EditorPlatformBuildScenePackager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            PlatformDefinition platformDefinition,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver = null)
            : this(projectRootPath, importers, platformDefinition?.PlatformId, platformDefinition, defaultFontAsset, scriptTypeResolver) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, platform id, optional platform definition, and default font asset.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="targetPlatformId">Platform id that should be reported to the asset-import pipeline.</param>
        /// <param name="platformDefinition">Optional builder-provided platform definition that carries compatibility metadata.</param>
        /// <param name="defaultFontAsset">Packaged default font asset used by player builds.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        EditorPlatformBuildScenePackager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            string targetPlatformId,
            PlatformDefinition platformDefinition,
            FontAsset defaultFontAsset,
            IScriptTypeResolver scriptTypeResolver)
            : this(
                projectRootPath,
                importers,
                targetPlatformId,
                platformDefinition,
                defaultFontAsset,
                null,
                string.Empty,
                string.Empty,
                scriptTypeResolver) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, target platform id, and builder-owned material translator.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="targetPlatformId">Platform id that should be reported to the asset-import pipeline.</param>
        /// <param name="materialBuilder">Builder used to translate schema-driven material settings during packaging.</param>
        /// <param name="selectedBuildProfileId">Selected build profile id for the current packaging operation.</param>
        /// <param name="selectedGraphicsProfileId">Selected graphics profile id for the current packaging operation.</param>
        public EditorPlatformBuildScenePackager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            string targetPlatformId,
            IPlatformAssetBuilder materialBuilder,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId)
            : this(
                projectRootPath,
                importers,
                targetPlatformId,
                null,
                null,
                materialBuilder,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                null) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, platform definition, default font asset, and optional material translator.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="platformDefinition">Builder-provided platform definition that carries compatibility metadata.</param>
        /// <param name="defaultFontAsset">Packaged default font asset used by player builds.</param>
        /// <param name="materialBuilder">Builder used to translate schema-driven material settings during packaging.</param>
        /// <param name="selectedBuildProfileId">Selected build profile id for the current packaging operation.</param>
        /// <param name="selectedGraphicsProfileId">Selected graphics profile id for the current packaging operation.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        public EditorPlatformBuildScenePackager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            PlatformDefinition platformDefinition,
            FontAsset defaultFontAsset,
            IPlatformAssetBuilder materialBuilder,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            IScriptTypeResolver scriptTypeResolver = null)
            : this(
                projectRootPath,
                importers,
                platformDefinition?.PlatformId,
                platformDefinition,
                defaultFontAsset,
                materialBuilder,
                selectedBuildProfileId,
                selectedGraphicsProfileId,
                scriptTypeResolver) {
        }

        /// <summary>
        /// Initializes one scene packager for the supplied project root, importer registrations, platform id, optional platform definition, and optional material translator.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="targetPlatformId">Platform id that should be reported to the asset-import pipeline.</param>
        /// <param name="platformDefinition">Optional builder-provided platform definition that carries compatibility metadata.</param>
        /// <param name="defaultFontAsset">Packaged default font asset used by player builds.</param>
        /// <param name="materialBuilder">Builder used to translate schema-driven material settings during packaging.</param>
        /// <param name="selectedBuildProfileId">Selected build profile id for the current packaging operation.</param>
        /// <param name="selectedGraphicsProfileId">Selected graphics profile id for the current packaging operation.</param>
        /// <param name="scriptTypeResolver">Optional shared script type resolver used for loaded gameplay modules.</param>
        EditorPlatformBuildScenePackager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            string targetPlatformId,
            PlatformDefinition platformDefinition,
            FontAsset defaultFontAsset,
            IPlatformAssetBuilder materialBuilder,
            string selectedBuildProfileId,
            string selectedGraphicsProfileId,
            IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            ProjectContentManager = new ContentManager(AssetsRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(ProjectContentManager);
            DefaultFontAsset = defaultFontAsset;
            TargetPlatformId = string.IsNullOrWhiteSpace(targetPlatformId) ? "windows" : targetPlatformId;
            MaterialAssetSettingsService = new MaterialAssetSettingsService();
            MaterialBuilder = materialBuilder;
            SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
            SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;

            ContentManager importContentManager = new ContentManager(AssetsRootPath);
            AssetImportManager = new AssetImportManager(ProjectRootPath, importContentManager);
            AssetImportManager.CurrentPlatformId = TargetPlatformId;
            Importers = importers;
            for (int index = 0; index < Importers.Count; index++) {
                IAssetImporterRegistration registration = Importers[index];
                if (registration == null) {
                    throw new InvalidOperationException("Importer registrations must not contain null entries.");
                }

                registration.Register(AssetImportManager);
            }
            FileSystemModelResolver = new EditorFileSystemModelResolver(AssetImportManager);
            ReferencedShaderAssetIds = new List<string>();
            ReferencedShaderAssetIdsSet = new HashSet<string>(StringComparer.Ordinal);
            PlatformId = string.IsNullOrWhiteSpace(targetPlatformId) ? "windows" : targetPlatformId;
            ComponentCompatibilitiesByTypeId = BuildEffectiveCompatibilityLookup(platformDefinition?.ComponentCompatibilities);
            TransformService = new SceneComponentPackagingTransformService(
                AssetsRootPath,
                ProjectContentManager,
                AssetImportManager,
                FileSystemModelResolver,
                ReferencedShaderAssetIds,
                ReferencedShaderAssetIdsSet,
                TargetPlatformId,
                MaterialBuilder,
                SelectedBuildProfileId,
                SelectedGraphicsProfileId,
                scriptTypeResolver);
        }

        /// <summary>
        /// Packages the selected scenes and the assets they require into the supplied build root.
        /// </summary>
        /// <param name="sceneIds">Project-relative scene ids selected for the build.</param>
        /// <param name="buildRootPath">Absolute build root path that will host the packaged content.</param>
        /// <returns>Scene-packaging result that carries the referenced shader ids.</returns>
        public EditorPlatformBuildScenePackagerResult Package(IReadOnlyList<string> sceneIds, string buildRootPath) {
            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }
            if (sceneIds.Count == 0) {
                throw new InvalidOperationException($"At least one scene must be selected for platform '{PlatformId}'.");
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            string fullBuildRootPath = Path.GetFullPath(buildRootPath);
            Directory.CreateDirectory(fullBuildRootPath);

            ReferencedShaderAssetIds.Clear();
            ReferencedShaderAssetIdsSet.Clear();
            EnsureGeneratedStandardMaterialAssets(fullBuildRootPath);

            for (int index = 0; index < sceneIds.Count; index++) {
                string sceneId = sceneIds[index];
                SceneAsset packagedSceneAsset = LoadSceneAsset(sceneId);
                RewriteSceneAsset(packagedSceneAsset, fullBuildRootPath);

                string packagedSceneRelativePath = BuildPackagedSceneRelativePath(sceneId, index);
                WriteAsset(Path.Combine(fullBuildRootPath, packagedSceneRelativePath), packagedSceneAsset);
            }

            return new EditorPlatformBuildScenePackagerResult(ReferencedShaderAssetIds);
        }

        /// <summary>
        /// Loads one selected scene asset from the source project.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id to load.</param>
        /// <returns>Loaded serialized scene asset.</returns>
        SceneAsset LoadSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string fullScenePath = ResolveProjectAssetPath(sceneId);
            try {
                using FileStream stream = File.OpenRead(fullScenePath);
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is not SceneAsset sceneAsset) {
                    throw new InvalidOperationException($"Scene '{sceneId}' did not deserialize into a SceneAsset.");
                }

                return sceneAsset;
            } catch (Exception ex) when (ex is not InvalidOperationException || !ex.Message.Contains(sceneId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Scene '{sceneId}' at '{fullScenePath}' could not be deserialized.", ex);
            }
        }

        /// <summary>
        /// Rewrites one serialized scene asset in place so it targets packaged runtime files instead of editor-only references.
        /// </summary>
        /// <param name="sceneAsset">Scene asset to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void RewriteSceneAsset(SceneAsset sceneAsset, string buildRootPath) {
            if (sceneAsset == null) {
                throw new ArgumentNullException(nameof(sceneAsset));
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            SceneEntityAsset[] rootEntityAssets = sceneAsset.RootEntities ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                RewriteEntityAsset(rootEntityAssets[index], buildRootPath);
            }

            SceneAssetReference[] assetReferences = sceneAsset.AssetReferences ?? Array.Empty<SceneAssetReference>();
            List<SceneAssetReference> rewrittenAssetReferences = new List<SceneAssetReference>(assetReferences.Length);
            for (int index = 0; index < assetReferences.Length; index++) {
                rewrittenAssetReferences.Add(RewriteSceneAssetReference(assetReferences[index], buildRootPath));
            }

            sceneAsset.AssetReferences = rewrittenAssetReferences.ToArray();
            sceneAsset.Physics3DSceneFeatureFlags = (uint)PhysicsSceneFeatureAnalyzer3D.Analyze(sceneAsset);
        }

        /// <summary>
        /// Rewrites one serialized scene entity recursively.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        void RewriteEntityAsset(SceneEntityAsset entityAsset, string buildRootPath) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            ApplyTargetPlatformTransformOverride(entityAsset);
            ApplyTargetPlatformComponentOverrides(entityAsset);
            SceneComponentAssetRecord[] componentRecords = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < componentRecords.Length; index++) {
                componentRecords[index] = RewriteComponentRecord(componentRecords[index], buildRootPath);
            }

            SceneEntityAsset[] childEntityAssets = entityAsset.Children ?? Array.Empty<SceneEntityAsset>();
            for (int index = 0; index < childEntityAssets.Length; index++) {
                RewriteEntityAsset(childEntityAssets[index], buildRootPath);
            }
        }

        /// <summary>
        /// Applies the selected target platform transform override to one serialized scene entity before the runtime scene is written.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload being packaged.</param>
        void ApplyTargetPlatformTransformOverride(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            SceneEntityPlatformTransformOverrideAsset transformOverride = FindTargetPlatformTransformOverride(entityAsset);
            if (transformOverride != null) {
                if (transformOverride.HasLocalPositionOverride) {
                    entityAsset.LocalPosition = transformOverride.LocalPosition;
                }
                if (transformOverride.HasLocalScaleOverride) {
                    entityAsset.LocalScale = transformOverride.LocalScale;
                }
                if (transformOverride.HasLocalOrientationOverride) {
                    entityAsset.LocalOrientation = transformOverride.LocalOrientation;
                }
            }

            entityAsset.PlatformTransformOverrides = Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
        }

        /// <summary>
        /// Applies the selected target platform component existence override to one serialized scene entity before the runtime scene is written.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload being packaged.</param>
        void ApplyTargetPlatformComponentOverrides(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            SceneEntityPlatformComponentOverrideAsset componentOverride = FindTargetPlatformComponentOverride(entityAsset);
            if (componentOverride == null) {
                entityAsset.PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
                return;
            }

            HashSet<string> removedComponentKeys = new HashSet<string>(
                (componentOverride.RemovedComponentKeys ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.Ordinal);

            List<SceneComponentAssetRecord> effectiveComponents = new List<SceneComponentAssetRecord>();
            SceneComponentAssetRecord[] authoredComponents = entityAsset.Components ?? Array.Empty<SceneComponentAssetRecord>();
            for (int index = 0; index < authoredComponents.Length; index++) {
                SceneComponentAssetRecord componentRecord = authoredComponents[index];
                if (componentRecord == null) {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(componentRecord.ComponentKey) && removedComponentKeys.Contains(componentRecord.ComponentKey)) {
                    continue;
                }

                effectiveComponents.Add(componentRecord);
            }

            SceneEntityPlatformAddedComponentAsset[] addedComponents = componentOverride.AddedComponents ?? Array.Empty<SceneEntityPlatformAddedComponentAsset>();
            for (int index = 0; index < addedComponents.Length; index++) {
                if (addedComponents[index]?.Component != null) {
                    effectiveComponents.Add(addedComponents[index].Component);
                }
            }

            for (int index = 0; index < effectiveComponents.Count; index++) {
                effectiveComponents[index].ComponentIndex = index;
            }

            entityAsset.Components = effectiveComponents.ToArray();
            entityAsset.PlatformComponentOverrides = Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
        }

        /// <summary>
        /// Resolves the transform override that matches the current packaging target platform.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload whose transform override should be resolved.</param>
        /// <returns>Matching target-platform transform override when one exists; otherwise null.</returns>
        SceneEntityPlatformTransformOverrideAsset FindTargetPlatformTransformOverride(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            if (string.IsNullOrWhiteSpace(TargetPlatformId) ||
                string.Equals(TargetPlatformId, EntityPlatformTransformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            SceneEntityPlatformTransformOverrideAsset[] transformOverrides = entityAsset.PlatformTransformOverrides ?? Array.Empty<SceneEntityPlatformTransformOverrideAsset>();
            for (int index = 0; index < transformOverrides.Length; index++) {
                SceneEntityPlatformTransformOverrideAsset transformOverride = transformOverrides[index];
                if (transformOverride == null || string.IsNullOrWhiteSpace(transformOverride.PlatformId)) {
                    continue;
                }

                if (string.Equals(transformOverride.PlatformId, TargetPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return transformOverride;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the component existence override that matches the current packaging target platform.
        /// </summary>
        /// <param name="entityAsset">Scene entity payload whose component existence override should be resolved.</param>
        /// <returns>Matching target-platform component existence override when one exists; otherwise null.</returns>
        SceneEntityPlatformComponentOverrideAsset FindTargetPlatformComponentOverride(SceneEntityAsset entityAsset) {
            if (entityAsset == null) {
                throw new ArgumentNullException(nameof(entityAsset));
            }

            if (string.IsNullOrWhiteSpace(TargetPlatformId) ||
                string.Equals(TargetPlatformId, EntityPlatformTransformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            SceneEntityPlatformComponentOverrideAsset[] componentOverrides = entityAsset.PlatformComponentOverrides ?? Array.Empty<SceneEntityPlatformComponentOverrideAsset>();
            for (int index = 0; index < componentOverrides.Length; index++) {
                SceneEntityPlatformComponentOverrideAsset componentOverride = componentOverrides[index];
                if (componentOverride == null || string.IsNullOrWhiteSpace(componentOverride.PlatformId)) {
                    continue;
                }

                if (string.Equals(componentOverride.PlatformId, TargetPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return componentOverride;
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures one scene-level asset reference is exported into the packaged build root.
        /// </summary>
        /// <param name="reference">Scene-level asset reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        SceneAssetReference RewriteSceneAssetReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                return null;
            }
            if (string.IsNullOrWhiteSpace(buildRootPath)) {
                throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                string fullPath = ResolveProjectAssetPath(reference.RelativePath);
                string fullExtension = Path.GetExtension(fullPath);
                if (AssetImportManager.IsModelExtension(fullExtension)) {
                    if (!AssetImportManager.TryLoadModelAsset(fullPath, out ModelAsset modelAsset) || modelAsset == null) {
                        throw new InvalidOperationException($"Model reference '{reference.RelativePath}' could not be imported into a packaged model asset.");
                    }

                    string importedModelRelativePath = BuildImportedModelRelativePath(reference.RelativePath);
                    WriteAsset(Path.Combine(buildRootPath, importedModelRelativePath), modelAsset);
                    return CreateFileSystemReference(importedModelRelativePath);
                }

                if (AssetImportManager.IsFontExtension(fullExtension)) {
                    return RewriteFileSystemFontReference(reference, buildRootPath);
                }

                string copiedRelativePath = NormalizeRelativePath(reference.RelativePath);
                CopyFile(fullPath, Path.Combine(buildRootPath, copiedRelativePath));
                return CreateFileSystemReference(copiedRelativePath);
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                if (string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal) &&
                    string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    WriteFontAsset(Path.Combine(buildRootPath, EditorFontRelativePath), DefaultFontAsset);
                    return CreateFileSystemReference(EditorFontRelativePath);
                }

                if (string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal) &&
                    string.Equals(reference.AssetId, StandardGeneratedMaterialAssetId, StringComparison.Ordinal)) {
                    EnsureGeneratedStandardMaterialAssets(buildRootPath);
                    return CreateFileSystemReference(StandardGeneratedMaterialRelativePath);
                }

                if (string.Equals(reference.ProviderId, EngineGeneratedProviderId, StringComparison.Ordinal) &&
                    (string.Equals(reference.AssetId, CubeGeneratedAssetId, StringComparison.Ordinal) ||
                     string.Equals(reference.AssetId, PlaneGeneratedAssetId, StringComparison.Ordinal) ||
                     string.Equals(reference.AssetId, SphereGeneratedAssetId, StringComparison.Ordinal))) {
                    return RewriteGeneratedModelReference(reference, buildRootPath);
                }

                throw new InvalidOperationException($"Unsupported generated scene asset reference '{reference.ProviderId}:{reference.AssetId}'.");
            }

            throw new InvalidOperationException($"Unsupported scene asset reference source kind '{reference.SourceKind}'.");
        }

        /// <summary>
        /// Rewrites one serialized component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten component record.</returns>
        SceneComponentAssetRecord RewriteComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            PlatformComponentCompatibilityDefinition compatibility = GetComponentCompatibility(record.ComponentTypeId);
            if (compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.PassThrough) {
                return record;
            }

            if (compatibility.CompatibilityKind == PlatformComponentCompatibilityKind.Transform) {
                if (TransformService.TryTransform(record, buildRootPath, out SceneComponentAssetRecord transformedRecord)) {
                    return transformedRecord;
                }

                throw new InvalidOperationException(BuildUnsupportedTransformMessage(record.ComponentTypeId));
            }

            throw new InvalidOperationException(BuildUnsupportedComponentMessage(record.ComponentTypeId, compatibility));
        }

        /// <summary>
        /// Resolves the compatibility definition for one serialized component type id.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        /// <returns>Matching compatibility definition.</returns>
        PlatformComponentCompatibilityDefinition GetComponentCompatibility(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            if (!ComponentCompatibilitiesByTypeId.TryGetValue(componentTypeId, out PlatformComponentCompatibilityDefinition compatibility)) {
                if (TransformService.CanTransform(componentTypeId)) {
                    return new PlatformComponentCompatibilityDefinition(
                        componentTypeId,
                        PlatformComponentCompatibilityKind.Transform,
                        "Eligible reflected components are rewritten into packaged ordinal payloads.",
                        string.Empty);
                }

                throw new InvalidOperationException($"Platform '{PlatformId}' does not declare compatibility for component '{componentTypeId}'.");
            }

            return compatibility;
        }

        /// <summary>
        /// Builds one compatibility lookup that preserves the built-in defaults while allowing builder-provided entries to override them.
        /// </summary>
        /// <param name="componentCompatibilities">Optional builder-provided compatibility entries.</param>
        /// <returns>Case-insensitive compatibility lookup.</returns>
        static Dictionary<string, PlatformComponentCompatibilityDefinition> BuildEffectiveCompatibilityLookup(
            IReadOnlyList<PlatformComponentCompatibilityDefinition> componentCompatibilities) {
            Dictionary<string, PlatformComponentCompatibilityDefinition> lookup =
                new Dictionary<string, PlatformComponentCompatibilityDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, PlatformComponentCompatibilityDefinition> defaultCompatibilityLookup =
                new Dictionary<string, PlatformComponentCompatibilityDefinition>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> builderCompatibilityTypeIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            PlatformComponentCompatibilityDefinition[] defaultCompatibilities = CreateDefaultComponentCompatibilities();
            for (int index = 0; index < defaultCompatibilities.Length; index++) {
                PlatformComponentCompatibilityDefinition compatibility = defaultCompatibilities[index];
                lookup.Add(compatibility.ComponentTypeId, compatibility);
                defaultCompatibilityLookup.Add(compatibility.ComponentTypeId, compatibility);
            }

            if (componentCompatibilities == null) {
                return lookup;
            }

            for (int index = 0; index < componentCompatibilities.Count; index++) {
                PlatformComponentCompatibilityDefinition compatibility = componentCompatibilities[index];
                if (compatibility == null) {
                    throw new InvalidOperationException("Platform compatibility metadata must not contain null entries.");
                }
                if (!builderCompatibilityTypeIds.Add(compatibility.ComponentTypeId)) {
                    throw new InvalidOperationException($"Platform compatibility metadata already contains an entry for '{compatibility.ComponentTypeId}'.");
                }

                if (ShouldPreserveDefaultCompatibility(defaultCompatibilityLookup, compatibility)) {
                    continue;
                }

                lookup[compatibility.ComponentTypeId] = compatibility;
            }

            return lookup;
        }

        /// <summary>
        /// Returns true when one builder-provided compatibility would weaken a required built-in transform contract.
        /// </summary>
        /// <param name="defaultCompatibilityLookup">Built-in default compatibility metadata keyed by serialized component type id.</param>
        /// <param name="builderCompatibility">Builder-provided compatibility metadata under evaluation.</param>
        /// <returns>True when the built-in compatibility must be preserved; otherwise false.</returns>
        static bool ShouldPreserveDefaultCompatibility(
            IReadOnlyDictionary<string, PlatformComponentCompatibilityDefinition> defaultCompatibilityLookup,
            PlatformComponentCompatibilityDefinition builderCompatibility) {
            if (defaultCompatibilityLookup == null) {
                throw new ArgumentNullException(nameof(defaultCompatibilityLookup));
            } else if (builderCompatibility == null) {
                throw new ArgumentNullException(nameof(builderCompatibility));
            }

            if (!defaultCompatibilityLookup.TryGetValue(builderCompatibility.ComponentTypeId, out PlatformComponentCompatibilityDefinition defaultCompatibility)) {
                return false;
            }

            return defaultCompatibility.CompatibilityKind == PlatformComponentCompatibilityKind.Transform
                && builderCompatibility.CompatibilityKind == PlatformComponentCompatibilityKind.PassThrough;
        }

        /// <summary>
        /// Builds the built-in component compatibility defaults used by the current constructor paths.
        /// </summary>
        /// <returns>Default shared component compatibility entries.</returns>
        static PlatformComponentCompatibilityDefinition[] CreateDefaultComponentCompatibilities() {
            return [
                new PlatformComponentCompatibilityDefinition(
                    MeshComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Mesh components are normalized during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    CameraComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Camera components are normalized during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    FPSComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "FPS overlay font references are rewritten during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    TextComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Text component font references are rewritten during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    RigidBody3DComponentTypeId,
                    PlatformComponentCompatibilityKind.PassThrough,
                    "3D rigid-body components are emitted unchanged for the current runtime loader.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    BoxCollider3DComponentTypeId,
                    PlatformComponentCompatibilityKind.PassThrough,
                    "3D box collider components are emitted unchanged for the current runtime loader.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    KinematicMotion3DComponentTypeId,
                    PlatformComponentCompatibilityKind.PassThrough,
                    "3D kinematic motion components are emitted unchanged for the current runtime loader.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    CharacterController3DComponentTypeId,
                    PlatformComponentCompatibilityKind.PassThrough,
                    "3D character-controller components are emitted unchanged for the current runtime loader.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    "helengine.RoundedRectComponent",
                    PlatformComponentCompatibilityKind.Transform,
                    "Rounded rectangle visuals are rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    "helengine.DirectionalLightComponent",
                    PlatformComponentCompatibilityKind.Transform,
                    "Directional light payloads are rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    "helengine.PointLightComponent",
                    PlatformComponentCompatibilityKind.Transform,
                    "Point light payloads are rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    "helengine.SpotLightComponent",
                    PlatformComponentCompatibilityKind.Transform,
                    "Spot light payloads are rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    MenuComponent.SerializedComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Baked demo menu root components are rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    MenuPanelComponent.SerializedComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Baked demo menu panel metadata is rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    MenuItemComponent.SerializedComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Baked demo menu item metadata is rewritten into strict runtime payloads during packaging.",
                    string.Empty),
                new PlatformComponentCompatibilityDefinition(
                    MenuSelectedDescriptionComponent.SerializedComponentTypeId,
                    PlatformComponentCompatibilityKind.Transform,
                    "Baked demo menu selected-description markers are rewritten into strict runtime payloads during packaging.",
                    string.Empty)
            ];
        }

        /// <summary>
        /// Builds the diagnostic message used when a component is explicitly unsupported.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        /// <param name="compatibility">Compatibility metadata supplied by the builder.</param>
        /// <returns>Formatted diagnostic message.</returns>
        static string BuildUnsupportedComponentMessage(string componentTypeId, PlatformComponentCompatibilityDefinition compatibility) {
            if (compatibility == null) {
                throw new ArgumentNullException(nameof(compatibility));
            }

            string message = string.IsNullOrWhiteSpace(compatibility.Reason)
                ? $"Platform does not support serialized component type '{componentTypeId}'."
                : compatibility.Reason;

            if (!string.IsNullOrWhiteSpace(compatibility.Remediation)) {
                message = string.Concat(message, " ", compatibility.Remediation);
            }

            return message;
        }

        /// <summary>
        /// Builds the diagnostic message used when a transform was requested but not implemented.
        /// </summary>
        /// <param name="componentTypeId">Serialized component type id.</param>
        /// <returns>Formatted diagnostic message.</returns>
        static string BuildUnsupportedTransformMessage(string componentTypeId) {
            return $"No shared packaging transform is available for component '{componentTypeId}'.";
        }

        /// <summary>
        /// Rewrites one serialized mesh component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Serialized mesh component record to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Rewritten mesh component record.</returns>
        SceneComponentAssetRecord RewriteMeshComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            MeshComponentScenePayloadSerializer.Read(reader, out SceneAssetReference modelReference, out SceneAssetReference[] materialReferences, out byte renderOrder3D);

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
        /// Rewrites one serialized camera component record into the runtime layer space expected by the Windows player.
        /// </summary>
        /// <param name="record">Serialized camera component record to rewrite.</param>
        /// <returns>Rewritten camera component record.</returns>
        SceneComponentAssetRecord RewriteCameraComponentRecord(SceneComponentAssetRecord record) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CameraComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported camera component payload version '{version}'.");
            }

            byte cameraDrawOrder = reader.ReadByte();
            ushort layerMask = reader.ReadUInt16();
            float4 viewport = ReadFloat4(reader);
            float nearPlaneDistance = reader.ReadSingle();
            float farPlaneDistance = reader.ReadSingle();
            CameraClearSettings clearSettings = ReadClearSettings(reader);
            CameraRenderSettings renderSettings = ReadRenderSettings(reader);

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CameraComponentPayloadVersion);
            writer.WriteByte(cameraDrawOrder);
            writer.WriteUInt16(NormalizePackagedCameraLayerMask(layerMask));
            WriteFloat4(writer, viewport);
            writer.WriteSingle(nearPlaneDistance);
            writer.WriteSingle(farPlaneDistance);
            WriteClearSettings(writer, clearSettings);
            WriteRenderSettings(writer, renderSettings);

            return new SceneComponentAssetRecord {
                ComponentTypeId = record.ComponentTypeId,
                ComponentIndex = record.ComponentIndex,
                Payload = writeStream.ToArray()
            };
        }

        /// <summary>
        /// Rewrites one serialized FPS component record into its packaged runtime form.
        /// </summary>
        /// <param name="record">Serialized FPS component record to rewrite.</param>
        /// <returns>Rewritten FPS component record.</returns>
        SceneComponentAssetRecord RewriteFPSComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != FPSComponentPayloadVersion) {
                throw new InvalidOperationException($"Unsupported FPS component payload version '{version}'.");
            }

            SceneAssetReference fontReference = ReadOptionalReference(reader);
            SceneAssetReference rewrittenFontReference = RewriteFontReference(fontReference, buildRootPath);
            double refreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64());
            int2 padding = reader.ReadInt2();
            byte renderOrder2D = reader.ReadByte();

            using MemoryStream writeStream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(FPSComponentPayloadVersion);
            WriteOptionalReference(writer, rewrittenFontReference);
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
        /// Converts one font reference to its packaged runtime form.
        /// </summary>
        /// <param name="reference">Font reference to rewrite.</param>
        /// <returns>Packaged runtime font reference.</returns>
        SceneAssetReference RewriteFontReference(SceneAssetReference reference, string buildRootPath) {
            if (reference == null) {
                throw new InvalidOperationException("FPSComponent requires a font reference before packaging.");
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                if (string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal) &&
                    string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    WriteFontAsset(Path.Combine(buildRootPath, EditorFontRelativePath), DefaultFontAsset);
                    return CreateFileSystemReference(EditorFontRelativePath);
                }

                throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}:{reference.AssetId}'.");
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
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hasset",
                ProviderId = EditorGeneratedProviderId,
                AssetId = EditorFontAssetId
            };
        }

        /// <summary>
        /// Rewrites one serialized model reference into a packaged file-backed scene reference.
        /// </summary>
        /// <param name="reference">Serialized model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed model reference, or null when the serialized reference was null.</returns>
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

        /// <summary>
        /// Rewrites one serialized material reference into a packaged file-backed scene reference.
        /// </summary>
        /// <param name="reference">Serialized material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed material reference, or null when the serialized reference was null.</returns>
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

        /// <summary>
        /// Rewrites one engine-generated model reference into a packaged file-backed model asset.
        /// </summary>
        /// <param name="reference">Generated model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the generated model.</returns>
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
                return CreateFileSystemReference(cookedRelativePath);
            }

            FontAsset fontAsset = LoadImportedFontAssetForPackaging(reference, sourcePath);
            WriteFontAsset(Path.Combine(buildRootPath, cookedRelativePath), fontAsset);
            return CreateFileSystemReference(cookedRelativePath);
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
        /// Rewrites one file-backed source-model reference into a packaged processed `ModelAsset`.
        /// </summary>
        /// <param name="reference">File-backed model reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the processed model asset.</returns>
        SceneAssetReference RewriteFileSystemModelReference(SceneAssetReference reference, string buildRootPath) {
            string sourcePath = ResolveProjectAssetPath(reference.RelativePath);
            ModelAsset modelAsset = FileSystemModelResolver.ResolveModelAsset(sourcePath);
            string relativePath = BuildImportedModelRelativePath(reference.RelativePath);
            WriteAsset(Path.Combine(buildRootPath, relativePath), modelAsset);
            return CreateFileSystemReference(relativePath);
        }

        /// <summary>
        /// Rewrites one engine-generated standard material reference into a packaged file-backed material asset.
        /// </summary>
        /// <param name="reference">Generated material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the generated material.</returns>
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

        /// <summary>
        /// Rewrites one file-backed material reference by copying the serialized material asset and its required shader package.
        /// </summary>
        /// <param name="reference">File-backed material reference to rewrite.</param>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
        /// <returns>Packaged file-backed scene reference for the copied material asset.</returns>
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
                MaterialAssetSettingsService.ApplyPlatformMaterialFields(materialAsset, compatibilityMaterialSettings, TargetPlatformId);
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

            string sourcePath = ResolveImportedTextureAssetPath(diffuseTextureAssetId);
            TextureAsset textureAsset;
            try {
                textureAsset = ProjectContentManager.Load<TextureAsset>(sourcePath, EditorContentProcessorIds.TextureAsset);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Imported texture '{diffuseTextureAssetId}' at '{sourcePath}' could not be loaded for packaging.", ex);
            }

            string cookedRelativePath = BuildImportedTextureCookedRelativePath(diffuseTextureAssetId);
            WriteAsset(Path.Combine(buildRootPath, cookedRelativePath), textureAsset);
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

            return Path.Combine(ProjectRootPath, "cache", assetId);
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
        /// Resolves the builder selection model used to seed missing material settings during packaging.
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
        /// Returns true when one persisted material sidecar exposes valid settings for the requested platform.
        /// </summary>
        /// <param name="settings">Persisted material settings candidate.</param>
        /// <param name="platformId">Target platform whose settings should be validated.</param>
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

        /// <summary>
        /// Ensures the packaged generated standard material exists under the build root.
        /// </summary>
        /// <param name="buildRootPath">Absolute build root path that receives packaged assets.</param>
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

        /// <summary>
        /// Tracks one referenced shader asset id if it has not already been recorded.
        /// </summary>
        /// <param name="shaderAssetId">Referenced shader asset identifier.</param>
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

        /// <summary>
        /// Creates one packaged file-backed scene reference for the supplied relative path.
        /// </summary>
        /// <param name="relativePath">Packaged asset path relative to the build root.</param>
        /// <returns>File-backed packaged scene reference.</returns>
        SceneAssetReference CreateFileSystemReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = NormalizeRelativePath(relativePath),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Writes one packaged font asset to disk.
        /// </summary>
        /// <param name="fullPath">Absolute output path.</param>
        /// <param name="fontAsset">Font asset to serialize.</param>
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

        /// <summary>
        /// Resolves one project-relative asset path beneath the source `assets` folder.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>Absolute source asset path.</returns>
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

        /// <summary>
        /// Builds one packaged scene relative path for an authored scene id.
        /// </summary>
        /// <param name="sceneId">Project-relative scene id.</param>
        /// <param name="sceneIndex">Zero-based selection index used to reserve the canonical main-scene path.</param>
        /// <returns>Packaged scene relative path beneath the cooked scenes root.</returns>
        string BuildPackagedSceneRelativePath(string sceneId, int sceneIndex) {
            return PackagedScenePathResolver.BuildRelativePath(sceneId, sceneIndex);
        }

        /// <summary>
        /// Builds one packaged processed-model relative path for an authored source-model reference.
        /// </summary>
        /// <param name="relativePath">Original project-relative source-model path.</param>
        /// <returns>Packaged processed-model relative path.</returns>
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
        /// Reads one optional scene asset reference from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the optional-reference payload.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
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

        /// <summary>
        /// Writes one optional scene asset reference to the current payload position.
        /// </summary>
        /// <param name="writer">Writer receiving the optional-reference payload.</param>
        /// <param name="reference">Optional scene asset reference to encode.</param>
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

        /// <summary>
        /// Reads one <see cref="float4"/> value from the current payload position.
        /// </summary>
        /// <param name="reader">Reader positioned at the vector payload.</param>
        /// <returns>Decoded <see cref="float4"/> value.</returns>
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

        /// <summary>
        /// Writes one <see cref="float4"/> value into the current payload.
        /// </summary>
        /// <param name="writer">Writer receiving the vector payload.</param>
        /// <param name="value">Vector value to encode.</param>
        void WriteFloat4(EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads one camera clear-settings payload from the current reader position.
        /// </summary>
        /// <param name="reader">Reader positioned at the clear-settings payload.</param>
        /// <returns>Decoded camera clear settings.</returns>
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

        /// <summary>
        /// Writes one camera clear-settings payload into the current writer position.
        /// </summary>
        /// <param name="writer">Writer receiving the clear-settings payload.</param>
        /// <param name="settings">Camera clear settings to encode.</param>
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

        /// <summary>
        /// Reads one camera render-settings payload from the current reader position.
        /// </summary>
        /// <param name="reader">Reader positioned at the render-settings payload.</param>
        /// <returns>Decoded camera render settings.</returns>
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

        /// <summary>
        /// Writes one camera render-settings payload into the current writer position.
        /// </summary>
        /// <param name="writer">Writer receiving the render-settings payload.</param>
        /// <param name="settings">Camera render settings to encode.</param>
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

        /// <summary>
        /// Normalizes one packaged scene-camera layer mask into the runtime scene layer used by the current Windows player loader.
        /// </summary>
        /// <param name="layerMask">Serialized authored camera layer mask.</param>
        /// <returns>Runtime layer mask used by packaged Windows players.</returns>
        ushort NormalizePackagedCameraLayerMask(ushort layerMask) {
            return RuntimeSceneLayerMask;
        }

        /// <summary>
        /// Writes one serialized asset payload to disk.
        /// </summary>
        /// <param name="fullPath">Absolute output path.</param>
        /// <param name="asset">Serialized asset to write.</param>
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
        /// Writes one cooked material byte payload to disk.
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

        /// <summary>
        /// Copies one file into the packaged build root, creating parent folders when required.
        /// </summary>
        /// <param name="sourcePath">Absolute source file path.</param>
        /// <param name="targetPath">Absolute packaged output path.</param>
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

        /// <summary>
        /// Normalizes one relative path to use forward slashes for persisted scene references.
        /// </summary>
        /// <param name="relativePath">Relative path to normalize.</param>
        /// <returns>Normalized forward-slash relative path.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Ensures one directory path ends with a trailing separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
