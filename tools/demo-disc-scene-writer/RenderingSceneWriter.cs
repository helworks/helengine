using helengine.files;
using helengine.editor;

namespace helengine.demo_disc_scene_writer {
    /// <summary>
    /// Writes committed rendering smoke scenes used by renderer validation and future demo-disc packaging.
    /// </summary>
    public sealed class RenderingSceneWriter {
        /// <summary>
        /// Stable serialized component identifier used by mesh records.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";
        /// <summary>
        /// Stable serialized component identifier used by camera records.
        /// </summary>
        const string CameraComponentTypeId = "helengine.CameraComponent";
        /// <summary>
        /// Stable serialized component identifier used by point-light records.
        /// </summary>
        const string PointLightComponentTypeId = "helengine.PointLightComponent";
        /// <summary>
        /// Stable serialized component identifier used by spot-light records.
        /// </summary>
        const string SpotLightComponentTypeId = "helengine.SpotLightComponent";
        /// <summary>
        /// Stable serialized component identifier used by directional-light records.
        /// </summary>
        const string DirectionalLightComponentTypeId = "helengine.DirectionalLightComponent";
        /// <summary>
        /// Stable generated engine provider identifier used by built-in assets.
        /// </summary>
        const string EngineProviderId = "engine";
        /// <summary>
        /// Stable generated engine cube-model asset identifier.
        /// </summary>
        const string CubeModelAssetId = "engine:model:cube";
        /// <summary>
        /// Stable generated engine plane-model asset identifier.
        /// </summary>
        const string PlaneModelAssetId = "engine:model:plane";
        /// <summary>
        /// Stable generated engine standard-material asset identifier.
        /// </summary>
        const string StandardMaterialAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId;
        /// <summary>
        /// Stable material importer identifier used by file-backed authored materials.
        /// </summary>
        const string MaterialImporterId = "helengine.material";
        /// <summary>
        /// Stable Windows standard-material schema identifier used by generated color-material settings.
        /// </summary>
        const string WindowsMaterialSchemaId = "standard-shader";
        /// <summary>
        /// Stable PS2 lit material schema identifier used by generated basis-test color materials.
        /// </summary>
        const string Ps2MaterialSchemaId = "ps2-simple-lit-textured";
        /// <summary>
        /// Stable standard shader asset identifier used by generated compatibility materials.
        /// </summary>
        const string StandardShaderAssetId = "ForwardStandardShader";
        /// <summary>
        /// Stable standard shader vertex program used by generated compatibility materials.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";
        /// <summary>
        /// Stable standard shader pixel program used by generated compatibility materials.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";
        /// <summary>
        /// Stable mesh variant used by generated compatibility materials.
        /// </summary>
        const string MeshVariantName = "Mesh";
        /// <summary>
        /// Stable material field identifier used to opt into standard-shader defaults on Windows.
        /// </summary>
        const string UseCustomShaderFieldId = "use-custom-shader";
        /// <summary>
        /// Stable material field identifier used for authored texture bindings on Windows.
        /// </summary>
        const string TextureIdFieldId = "texture-id";
        /// <summary>
        /// Stable material field identifier used for shadow-casting participation on Windows.
        /// </summary>
        const string CastsShadowFieldId = "casts-shadow";
        /// <summary>
        /// Stable material field identifier used for shadow receiving on Windows.
        /// </summary>
        const string ReceivesShadowFieldId = "receives-shadow";
        /// <summary>
        /// Stable material field identifier used for authored base color.
        /// </summary>
        const string BaseColorFieldId = "base-color";
        /// <summary>
        /// Stable PS2 material field identifier used for alpha mode.
        /// </summary>
        const string AlphaModeFieldId = "alpha-mode";
        /// <summary>
        /// Stable PS2 material field identifier used for double-sided control.
        /// </summary>
        const string DoubleSidedFieldId = "double-sided";
        /// <summary>
        /// Stable PS2 material field identifier used for shadow-casting participation.
        /// </summary>
        const string Ps2CastShadowsFieldId = "cast-shadows";
        /// <summary>
        /// Stable PS2 material field identifier used for vertex-color control.
        /// </summary>
        const string VertexColorModeFieldId = "vertex-color-mode";
        /// <summary>
        /// Layer mask used by user-authored scene objects in packaged runtime scenes.
        /// </summary>
        const ushort SceneObjectsLayerMask = 0b0100000000000000;

        /// <summary>
        /// Stable save-state slot name used for serialized mesh model references.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Stable save-state slot name used for serialized mesh material references.
        /// </summary>
        const string MeshMaterialReferenceName = "Material";

        /// <summary>
        /// Stable save-state slot name used for serialized font references.
        /// </summary>
        const string FontReferenceName = "Font";

        /// <summary>
        /// Scene id written into the committed point-shadow smoke scene asset.
        /// </summary>
        const string PointShadowSceneId = "Scenes/rendering/point-shadow.helen";
        /// <summary>
        /// Scene id written into the committed point-shadow lab scene asset.
        /// </summary>
        const string PointShadowLabSceneId = "Scenes/rendering/point-shadow-lab.helen";
        /// <summary>
        /// Scene id written into the committed spot-shadow lab scene asset.
        /// </summary>
        const string SpotShadowLabSceneId = "Scenes/rendering/spot-shadow-lab.helen";
        /// <summary>
        /// Scene id written into the committed directional-shadow lab scene asset.
        /// </summary>
        const string DirectionalShadowLabSceneId = "Scenes/rendering/directional-shadow-lab.helen";
        /// <summary>
        /// Scene id written into the committed PS2 basis and directional-light validation scene asset.
        /// </summary>
        const string Ps2BasisLightTestSceneId = "scenes/rendering/ps2_basis_light_test.helen";
        /// <summary>
        /// Scene id written into the committed directional-shadow plaza showcase scene asset.
        /// </summary>
        const string DirectionalShadowPlazaSceneId = DirectionalShadowPlazaSceneAssetFactory.SceneId;

        /// Descriptor used to serialize authored mesh payloads for committed editor scenes.
        /// </summary>
        readonly MeshComponentPersistenceDescriptor MeshDescriptor;

        /// <summary>
        /// Descriptor used to serialize authored point-light payloads for committed editor scenes.
        /// </summary>
        readonly PointLightComponentPersistenceDescriptor PointLightDescriptor;

        /// <summary>
        /// Descriptor used to serialize authored spot-light payloads for committed editor scenes.
        /// </summary>
        readonly SpotLightComponentPersistenceDescriptor SpotLightDescriptor;

        /// <summary>
        /// Descriptor used to serialize authored directional-light payloads for committed editor scenes.
        /// </summary>
        readonly DirectionalLightComponentPersistenceDescriptor DirectionalLightDescriptor;

        /// <summary>
        /// Placeholder runtime model used only to satisfy authored mesh serialization before stable asset references are applied.
        /// </summary>
        readonly AuthoringPlaceholderRuntimeModel PlaceholderModel;

        /// <summary>
        /// Placeholder runtime material used only to satisfy authored mesh serialization before stable asset references are applied.
        /// </summary>
        readonly RuntimeMaterial PlaceholderMaterial;

        /// <summary>
        /// Writes generated user-side runtime motion source files for rendering showcase scenes.
        /// </summary>
        readonly RenderingShowcaseSourceWriter ShowcaseSourceWriter;

        /// <summary>
        /// Builds the canonical directional-shadow plaza showcase scene asset.
        /// </summary>
        readonly DirectionalShadowPlazaSceneAssetFactory DirectionalShadowPlazaFactory;
        /// <summary>
        /// Writes generated material settings sidecars for file-backed authored materials.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialSettingsService;

        /// <summary>
        /// Descriptor used to serialize the FPS overlay component on each authored scene camera.
        /// </summary>
        readonly FPSComponentPersistenceDescriptor FpsDescriptor;

        /// <summary>
        /// Descriptor used to serialize reflected component payloads for fitted viewport overlay helpers.
        /// </summary>
        readonly AutomaticScriptComponentPersistenceDescriptor AutomaticDescriptor;

        /// <summary>
        /// Allocates numeric entity ids while one serialized rendering scene is being built.
        /// </summary>
        readonly SceneEntityAssetIdAllocator SceneEntityIdAllocator;

        /// <summary>
        /// Initializes the committed rendering scene writer with the persistence descriptors required for authored editor-scene output.
        /// </summary>
        public RenderingSceneWriter() {
            MeshDescriptor = new MeshComponentPersistenceDescriptor();
            PointLightDescriptor = new PointLightComponentPersistenceDescriptor();
            SpotLightDescriptor = new SpotLightComponentPersistenceDescriptor();
            DirectionalLightDescriptor = new DirectionalLightComponentPersistenceDescriptor();
            PlaceholderModel = new AuthoringPlaceholderRuntimeModel();
            PlaceholderMaterial = new RuntimeMaterial();
            ShowcaseSourceWriter = new RenderingShowcaseSourceWriter();
            DirectionalShadowPlazaFactory = new DirectionalShadowPlazaSceneAssetFactory();
            MaterialSettingsService = new MaterialAssetSettingsService();
            FpsDescriptor = new FPSComponentPersistenceDescriptor();
            AutomaticDescriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            SceneEntityIdAllocator = new SceneEntityAssetIdAllocator();
        }

        /// <summary>
        /// Writes the committed rendering smoke scenes into the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the committed `assets/Scenes/rendering` folder.</param>
        public void WriteAll(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            if (!Directory.Exists(assetsRootPath)) {
                throw new InvalidOperationException($"Assets root was not found: {assetsRootPath}");
            }

            ShowcaseSourceWriter.WriteDirectionalShadowPlazaSources(assetsRootPath);
            WritePs2BasisLightTestSceneAsset(assetsRootPath);
            WriteDirectionalShadowPlazaSceneAsset(assetsRootPath);
            WritePointShadowSceneAsset(assetsRootPath);
            WritePointShadowLabSceneAsset(assetsRootPath);
            WriteSpotShadowLabSceneAsset(assetsRootPath);
            WriteDirectionalShadowLabSceneAsset(assetsRootPath);
        }

        /// <summary>
        /// Writes the committed PS2 basis and directional-light validation scene asset.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WritePs2BasisLightTestSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, Ps2BasisLightTestSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("PS2 basis-light test scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            WritePs2BasisLightTestMaterialAssets(assetsRootPath);
            SceneEntityIdAllocator.Reset();
            SceneAsset sceneAsset = new SceneAsset {
                Id = Ps2BasisLightTestSceneId,
                AssetReferences = CreatePs2BasisLightTestAssetReferences(),
                RootEntities = CreatePs2BasisLightTestRootEntities()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes the committed directional-shadow plaza showcase scene asset.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WriteDirectionalShadowPlazaSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, DirectionalShadowPlazaSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Directional-shadow plaza scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneAsset sceneAsset = DirectionalShadowPlazaFactory.CreateSceneAsset(
                CreateGeneratedReference("Engine/Models/Plane", PlaneModelAssetId),
                CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId));

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes the committed point-shadow smoke scene asset.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WritePointShadowSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, PointShadowSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Point-shadow scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneEntityIdAllocator.Reset();
            SceneAsset sceneAsset = new SceneAsset {
                Id = PointShadowSceneId,
                AssetReferences = CreatePointShadowAssetReferences(),
                RootEntities = CreatePointShadowRootEntities()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes the committed point-shadow lab scene asset used for manual cubemap-face debugging.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WritePointShadowLabSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, PointShadowLabSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Point-shadow lab scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneEntityIdAllocator.Reset();
            SceneAsset sceneAsset = new SceneAsset {
                Id = PointShadowLabSceneId,
                AssetReferences = CreatePointShadowAssetReferences(),
                RootEntities = CreatePointShadowLabRootEntities()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes the committed spot-shadow lab scene asset used for manual cone-shadow debugging.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WriteSpotShadowLabSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, SpotShadowLabSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Spot-shadow lab scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneEntityIdAllocator.Reset();
            SceneAsset sceneAsset = new SceneAsset {
                Id = SpotShadowLabSceneId,
                AssetReferences = CreatePointShadowAssetReferences(),
                RootEntities = CreateSpotShadowLabRootEntities()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes the committed directional-shadow lab scene asset used for manual sun-shadow debugging.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WriteDirectionalShadowLabSceneAsset(string assetsRootPath) {
            string scenePath = Path.Combine(assetsRootPath, DirectionalShadowLabSceneId.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Directional-shadow lab scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneEntityIdAllocator.Reset();
            SceneAsset sceneAsset = new SceneAsset {
                Id = DirectionalShadowLabSceneId,
                AssetReferences = CreatePointShadowAssetReferences(),
                RootEntities = CreateDirectionalShadowLabRootEntities()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            EditorAssetBinarySerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates the stable scene asset references required by the committed point-shadow smoke scene.
        /// </summary>
        /// <returns>Stable scene asset references used by the scene meshes.</returns>
        SceneAssetReference[] CreatePointShadowAssetReferences() {
            return new[] {
                CreateGeneratedReference("Engine/Models/Plane", PlaneModelAssetId),
                CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId),
                CreateEditorFontReference()
            };
        }

        /// <summary>
        /// Creates the stable generated asset references required by the committed PS2 basis-light validation scene.
        /// </summary>
        /// <returns>Stable scene asset references used by the scene meshes.</returns>
        SceneAssetReference[] CreatePs2BasisLightTestAssetReferences() {
            return new[] {
                CreateGeneratedReference("Engine/Models/Plane", PlaneModelAssetId),
                CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                CreatePs2BasisLightTestMaterialReference(0),
                CreatePs2BasisLightTestMaterialReference(1),
                CreatePs2BasisLightTestMaterialReference(2),
                CreatePs2BasisLightTestMaterialReference(3),
                CreatePs2BasisLightTestMaterialReference(4),
                CreatePs2BasisLightTestMaterialReference(5),
                CreatePs2BasisLightTestMaterialReference(6),
                CreateEditorFontReference()
            };
        }

        /// <summary>
        /// Creates the root entity hierarchy stored in the committed point-shadow smoke scene.
        /// </summary>
        /// <returns>Serialized root entities for the scene.</returns>
        SceneEntityAsset[] CreatePointShadowRootEntities() {
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll(0f, -0.25f, 0f, out cameraOrientation);
            return new[] {
                CreateCameraEntity(
                    "point-shadow-camera",
                    "PointShadowCamera",
                    new float3(0f, 3f, -12f),
                    cameraOrientation),
                CreateFpsOverlayRootEntity("PointShadowFpsOverlayRoot"),
                CreateMeshEntity(
                    "point-shadow-floor",
                    "PointShadowFloor",
                    new float3(0f, -1f, 0f),
                    new float3(12f, 1f, 12f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Plane", PlaneModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-caster",
                    "PointShadowCaster",
                    new float3(0f, 0f, 0f),
                    new float3(2f, 2f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-receiver",
                    "PointShadowReceiver",
                    new float3(4f, 0f, 3f),
                    new float3(1.5f, 3f, 1.5f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreatePointLightEntity(
                    "point-shadow-light",
                    "PointShadowLight",
                    new float3(2f, 4f, -1f),
                    18f,
                    6f)
            };
        }

        /// <summary>
        /// Creates the root entity hierarchy stored in the committed point-shadow lab scene.
        /// </summary>
        /// <returns>Serialized root entities for the scene.</returns>
        SceneEntityAsset[] CreatePointShadowLabRootEntities() {
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll(0f, 0f, 0f, out cameraOrientation);
            return new[] {
                CreateCameraEntity(
                    "point-shadow-lab-camera",
                    "PointShadowLabCamera",
                    new float3(0f, 0f, -36f),
                    cameraOrientation),
                CreateFpsOverlayRootEntity("PointShadowLabFpsOverlayRoot"),
                CreateMeshEntity(
                    "point-shadow-lab-floor",
                    "PointShadowLabFloor",
                    new float3(0f, -24f, 0f),
                    new float3(48f, 2f, 48f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-ceiling",
                    "PointShadowLabCeiling",
                    new float3(0f, 24f, 0f),
                    new float3(48f, 2f, 48f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-wall-positive-x",
                    "PointShadowLabWallPositiveX",
                    new float3(24f, 0f, 0f),
                    new float3(2f, 48f, 48f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-wall-negative-x",
                    "PointShadowLabWallNegativeX",
                    new float3(-24f, 0f, 0f),
                    new float3(2f, 48f, 48f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-wall-positive-z",
                    "PointShadowLabWallPositiveZ",
                    new float3(0f, 0f, 24f),
                    new float3(48f, 48f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-wall-negative-z",
                    "PointShadowLabWallNegativeZ",
                    new float3(0f, 0f, -24f),
                    new float3(48f, 48f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-caster",
                    "PointShadowLabCaster",
                    new float3(8f, 0f, 6f),
                    new float3(6f, 6f, 6f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-x-top-left",
                    "PointShadowLabAxisMarkerPositiveXTopLeft",
                    new float3(20f, 8f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-x-bottom-left",
                    "PointShadowLabAxisMarkerPositiveXBottomLeft",
                    new float3(20f, -8f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-x-bottom-right",
                    "PointShadowLabAxisMarkerPositiveXBottomRight",
                    new float3(20f, -8f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-x-top-left",
                    "PointShadowLabAxisMarkerNegativeXTopLeft",
                    new float3(-20f, 8f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-x-bottom-left",
                    "PointShadowLabAxisMarkerNegativeXBottomLeft",
                    new float3(-20f, -8f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-x-bottom-right",
                    "PointShadowLabAxisMarkerNegativeXBottomRight",
                    new float3(-20f, -8f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-z-top-left",
                    "PointShadowLabAxisMarkerPositiveZTopLeft",
                    new float3(8f, 8f, 20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-z-bottom-left",
                    "PointShadowLabAxisMarkerPositiveZBottomLeft",
                    new float3(8f, -8f, 20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-z-bottom-right",
                    "PointShadowLabAxisMarkerPositiveZBottomRight",
                    new float3(-8f, -8f, 20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-z-top-left",
                    "PointShadowLabAxisMarkerNegativeZTopLeft",
                    new float3(-8f, 8f, -20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-z-bottom-left",
                    "PointShadowLabAxisMarkerNegativeZBottomLeft",
                    new float3(-8f, -8f, -20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-z-bottom-right",
                    "PointShadowLabAxisMarkerNegativeZBottomRight",
                    new float3(8f, -8f, -20f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-y-top-left",
                    "PointShadowLabAxisMarkerPositiveYTopLeft",
                    new float3(-8f, 20f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-y-bottom-left",
                    "PointShadowLabAxisMarkerPositiveYBottomLeft",
                    new float3(-8f, 20f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-positive-y-bottom-right",
                    "PointShadowLabAxisMarkerPositiveYBottomRight",
                    new float3(8f, 20f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-y-top-left",
                    "PointShadowLabAxisMarkerNegativeYTopLeft",
                    new float3(-8f, -20f, 8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-y-bottom-left",
                    "PointShadowLabAxisMarkerNegativeYBottomLeft",
                    new float3(-8f, -20f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "point-shadow-lab-axis-marker-negative-y-bottom-right",
                    "PointShadowLabAxisMarkerNegativeYBottomRight",
                    new float3(8f, -20f, -8f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreatePointLightEntity(
                    "point-shadow-lab-light",
                    "PointShadowLabLight",
                    new float3(0f, 0f, 0f),
                    56f,
                    8f)
            };
        }

        /// <summary>
        /// Creates the root entity hierarchy stored in the committed spot-shadow lab scene.
        /// </summary>
        /// <returns>Serialized root entities for the scene.</returns>
        SceneEntityAsset[] CreateSpotShadowLabRootEntities() {
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll(0f, 0f, 0f, out cameraOrientation);
            float4 spotLightOrientation;
            float4.CreateFromYawPitchRoll((float)(Math.PI * 0.15), (float)(-Math.PI * 0.16), 0f, out spotLightOrientation);
            return new[] {
                CreateCameraEntity(
                    "spot-shadow-lab-camera",
                    "SpotShadowLabCamera",
                    new float3(0f, 0f, -28f),
                    cameraOrientation),
                CreateFpsOverlayRootEntity("SpotShadowLabFpsOverlayRoot"),
                CreateMeshEntity(
                    "spot-shadow-lab-floor",
                    "SpotShadowLabFloor",
                    new float3(0f, -16f, 0f),
                    new float3(32f, 2f, 32f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-ceiling",
                    "SpotShadowLabCeiling",
                    new float3(0f, 16f, 0f),
                    new float3(32f, 2f, 32f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-wall-positive-x",
                    "SpotShadowLabWallPositiveX",
                    new float3(16f, 0f, 0f),
                    new float3(2f, 32f, 32f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-wall-negative-x",
                    "SpotShadowLabWallNegativeX",
                    new float3(-16f, 0f, 0f),
                    new float3(2f, 32f, 32f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-wall-positive-z",
                    "SpotShadowLabWallPositiveZ",
                    new float3(0f, 0f, 16f),
                    new float3(32f, 32f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-wall-negative-z",
                    "SpotShadowLabWallNegativeZ",
                    new float3(0f, 0f, -16f),
                    new float3(32f, 32f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-caster-primary",
                    "SpotShadowLabCasterPrimary",
                    new float3(4f, -8f, 6f),
                    new float3(4f, 8f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-caster-secondary",
                    "SpotShadowLabCasterSecondary",
                    new float3(-6f, -10f, 2f),
                    new float3(3f, 4f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-marker-positive-z-top-left",
                    "SpotShadowLabMarkerPositiveZTopLeft",
                    new float3(8f, 8f, 12f),
                    new float3(3f, 3f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-marker-positive-z-bottom-left",
                    "SpotShadowLabMarkerPositiveZBottomLeft",
                    new float3(8f, -8f, 12f),
                    new float3(3f, 3f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-marker-positive-z-bottom-right",
                    "SpotShadowLabMarkerPositiveZBottomRight",
                    new float3(-8f, -8f, 12f),
                    new float3(3f, 3f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "spot-shadow-lab-marker-positive-z-top-right",
                    "SpotShadowLabMarkerPositiveZTopRight",
                    new float3(-8f, 8f, 12f),
                    new float3(3f, 3f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateSpotLightEntity(
                    "spot-shadow-lab-light",
                    "SpotShadowLabLight",
                    new float3(-4f, 10f, -8f),
                    spotLightOrientation,
                    30f,
                    28f,
                    40f,
                    8f)
            };
        }

        /// <summary>
        /// Creates the root entity hierarchy stored in the committed directional-shadow lab scene.
        /// </summary>
        /// <returns>Serialized root entities for the scene.</returns>
        SceneEntityAsset[] CreateDirectionalShadowLabRootEntities() {
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll((float)(Math.PI * 0.08), (float)(-Math.PI * 0.18), 0f, out cameraOrientation);
            float4 directionalLightOrientation;
            float4.CreateFromYawPitchRoll((float)(-Math.PI * 0.27), (float)(-Math.PI * 0.24), 0f, out directionalLightOrientation);
            return new[] {
                CreateCameraEntity(
                    "directional-shadow-lab-camera",
                    "DirectionalShadowLabCamera",
                    new float3(-12f, 10f, -24f),
                    cameraOrientation),
                CreateFpsOverlayRootEntity("DirectionalShadowLabFpsOverlayRoot"),
                CreateMeshEntity(
                    "directional-shadow-lab-floor",
                    "DirectionalShadowLabFloor",
                    new float3(0f, -1f, 0f),
                    new float3(48f, 1f, 48f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-caster-left",
                    "DirectionalShadowLabCasterLeft",
                    new float3(-10f, 2f, -4f),
                    new float3(4f, 4f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-caster-center",
                    "DirectionalShadowLabCasterCenter",
                    new float3(0f, 4f, 0f),
                    new float3(4f, 8f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-caster-right",
                    "DirectionalShadowLabCasterRight",
                    new float3(10f, 3f, 6f),
                    new float3(4f, 6f, 4f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-column-front",
                    "DirectionalShadowLabColumnFront",
                    new float3(-4f, 6f, 14f),
                    new float3(3f, 12f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-column-back",
                    "DirectionalShadowLabColumnBack",
                    new float3(12f, 5f, -12f),
                    new float3(3f, 10f, 3f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-ground-marker-left",
                    "DirectionalShadowLabGroundMarkerLeft",
                    new float3(-18f, 0.5f, 12f),
                    new float3(2f, 1f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-ground-marker-center",
                    "DirectionalShadowLabGroundMarkerCenter",
                    new float3(-10f, 0.5f, 12f),
                    new float3(2f, 1f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateMeshEntity(
                    "directional-shadow-lab-ground-marker-right",
                    "DirectionalShadowLabGroundMarkerRight",
                    new float3(-10f, 0.5f, 20f),
                    new float3(2f, 1f, 2f),
                    float4.Identity,
                    CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId),
                    CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)),
                CreateDirectionalLightEntity(
                    "directional-shadow-lab-light",
                    "DirectionalShadowLabLight",
                    new float3(0f, 12f, 0f),
                    directionalLightOrientation,
                    2.8f,
                    60f)
            };
        }

        /// <summary>
        /// Creates the root entity hierarchy stored in the committed PS2 basis-light validation scene.
        /// </summary>
        /// <returns>Serialized root entities for the scene.</returns>
        SceneEntityAsset[] CreatePs2BasisLightTestRootEntities() {
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll(0f, -0.18f, 0f, out cameraOrientation);
            float4 directionalLightOrientation;
            float4.CreateFromYawPitchRoll(-0.95f, -0.55f, 0f, out directionalLightOrientation);

            SceneAssetReference planeReference = CreateGeneratedReference("Engine/Models/Plane", PlaneModelAssetId);
            SceneAssetReference cubeReference = CreateGeneratedReference("Engine/Models/Cube", CubeModelAssetId);
            SceneAssetReference groundMaterialReference = CreatePs2BasisLightTestMaterialReference(0);
            SceneAssetReference centerMaterialReference = CreatePs2BasisLightTestMaterialReference(1);
            SceneAssetReference plusXMaterialReference = CreatePs2BasisLightTestMaterialReference(2);
            SceneAssetReference minusXMaterialReference = CreatePs2BasisLightTestMaterialReference(3);
            SceneAssetReference plusZMaterialReference = CreatePs2BasisLightTestMaterialReference(4);
            SceneAssetReference minusZMaterialReference = CreatePs2BasisLightTestMaterialReference(5);
            SceneAssetReference cornerMaterialReference = CreatePs2BasisLightTestMaterialReference(6);

            return new[] {
                CreatePs2BasisLightTestCameraEntity(),
                CreateFpsOverlayRootEntity("Ps2BasisLightTestFpsOverlayRoot"),
                CreateMeshEntity(
                    "ps2-basis-light-test-ground",
                    "Ps2BasisLightTestGround",
                    new float3(0f, -1.5f, 0f),
                    new float3(8f, 1f, 8f),
                    float4.Identity,
                    planeReference,
                    groundMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-center-cube",
                    "Ps2BasisLightTestCenterCube",
                    new float3(0f, 0f, 0f),
                    new float3(1.75f, 1.75f, 1.75f),
                    float4.Identity,
                    cubeReference,
                    centerMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-plus-x-bar",
                    "Ps2BasisLightTestPlusXBar",
                    new float3(2.75f, 0f, 0f),
                    new float3(2.75f, 0.8f, 0.8f),
                    float4.Identity,
                    cubeReference,
                    plusXMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-minus-x-pillar",
                    "Ps2BasisLightTestMinusXPillar",
                    new float3(-2.75f, 0f, 0f),
                    new float3(0.8f, 2.75f, 0.8f),
                    float4.Identity,
                    cubeReference,
                    minusXMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-plus-z-bar",
                    "Ps2BasisLightTestPlusZBar",
                    new float3(0f, 0f, 2.75f),
                    new float3(0.8f, 0.8f, 2.75f),
                    float4.Identity,
                    cubeReference,
                    plusZMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-minus-z-slab",
                    "Ps2BasisLightTestMinusZSlab",
                    new float3(0f, -0.55f, -2.75f),
                    new float3(2.5f, 0.55f, 2.5f),
                    float4.Identity,
                    cubeReference,
                    minusZMaterialReference),
                CreateMeshEntity(
                    "ps2-basis-light-test-plus-x-plus-z-corner",
                    "Ps2BasisLightTestPlusXPlusZCorner",
                    new float3(2.2f, 0.45f, 2.2f),
                    new float3(1.1f, 1.1f, 1.1f),
                    float4.Identity,
                    cubeReference,
                    cornerMaterialReference),
                CreateDirectionalLightEntity(
                    "ps2-basis-light-test-light",
                    "Ps2BasisLightTestLight",
                    new float3(0f, 10f, 0f),
                    directionalLightOrientation,
                    2.0f,
                    48f)
            };
        }

        /// <summary>
        /// Creates the authored camera entity used by the PS2 basis-light validation scene.
        /// </summary>
        /// <returns>Serialized camera entity with the known-good rendering-scene payload contract.</returns>
        SceneEntityAsset CreatePs2BasisLightTestCameraEntity() {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "Ps2BasisLightTestCamera",
                LocalPosition = new float3(0f, 0f, 9f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = CameraComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WritePs2BasisLightTestCameraPayload()
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized camera entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localOrientation">Local orientation assigned to the entity.</param>
        /// <returns>Serialized camera entity.</returns>
        SceneEntityAsset CreateCameraEntity(string id, string name, float3 localPosition, float4 localOrientation) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = localOrientation,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = CameraComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WriteCameraPayload()
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one fitted HUD root that keeps the authored FPS overlay inside the shared reference-canvas scaling path.
        /// </summary>
        /// <param name="name">Display name stored on the overlay root entity.</param>
        /// <returns>Serialized fitted HUD root entity.</returns>
        SceneEntityAsset CreateFpsOverlayRootEntity(string name) {
            ViewportComponent viewportComponent = new ViewportComponent {
                BindingMode = ViewportComponent.ScreenBindingMode,
                FixedSize = new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight)
            };
            ReferenceCanvasFitComponent referenceCanvasFitComponent = new ReferenceCanvasFitComponent {
                ReferenceWidth = SceneCanvasProfile.DefaultWidth,
                ReferenceHeight = SceneCanvasProfile.DefaultHeight
            };

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    AutomaticDescriptor.SerializeComponent(viewportComponent, 0, null),
                    AutomaticDescriptor.SerializeComponent(referenceCanvasFitComponent, 1, null),
                    CreateFpsComponentRecord(2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized FPS overlay component record for a scene camera.
        /// </summary>
        /// <returns>Serialized FPS overlay component record.</returns>
        SceneComponentAssetRecord CreateFpsComponentRecord(int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "FPS component index must be non-negative.");
            }

            FPSComponent fpsComponent = new FPSComponent {
                Font = new FontAsset(new FontInfo("RenderingFpsPlaceholder", 16, 4f), null, new Dictionary<char, FontChar>(), 16f, 1, 1)
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(FontReferenceName, CreateEditorFontReference());
            return FpsDescriptor.SerializeComponent(fpsComponent, componentIndex, saveState);
        }

        /// <summary>
        /// Builds the stable scene asset reference for the editor's built-in font.
        /// </summary>
        /// <returns>Stable generated editor-font reference.</returns>
        SceneAssetReference CreateEditorFontReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "generated/editor/fonts/ui.hefont",
                ProviderId = "editor",
                AssetId = "ui-font"
            };
        }

        /// <summary>
        /// Creates one serialized mesh entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localScale">Local scale assigned to the entity.</param>
        /// <param name="localOrientation">Local orientation assigned to the entity.</param>
        /// <param name="modelReference">Stable generated model reference used by the mesh payload.</param>
        /// <param name="materialReference">Stable generated material reference used by the mesh payload.</param>
        /// <returns>Serialized mesh entity.</returns>
        SceneEntityAsset CreateMeshEntity(
            string id,
            string name,
            float3 localPosition,
            float3 localScale,
            float4 localOrientation,
            SceneAssetReference modelReference,
            SceneAssetReference materialReference) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = localPosition,
                LocalScale = localScale,
                LocalOrientation = localOrientation,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = MeshComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WriteMeshPayload(modelReference, materialReference)
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized point-light entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="range">Authored point-light range.</param>
        /// <param name="intensity">Authored point-light intensity.</param>
        /// <returns>Serialized point-light entity.</returns>
        SceneEntityAsset CreatePointLightEntity(string id, string name, float3 localPosition, float range, float intensity) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = PointLightComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WritePointLightPayload(range, intensity)
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized spot-light entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localOrientation">Local orientation assigned to the entity.</param>
        /// <param name="range">Authored spot-light range.</param>
        /// <param name="innerConeAngleDegrees">Authored inner cone angle in degrees.</param>
        /// <param name="outerConeAngleDegrees">Authored outer cone angle in degrees.</param>
        /// <param name="intensity">Authored spot-light intensity.</param>
        /// <returns>Serialized spot-light entity.</returns>
        SceneEntityAsset CreateSpotLightEntity(
            string id,
            string name,
            float3 localPosition,
            float4 localOrientation,
            float range,
            float innerConeAngleDegrees,
            float outerConeAngleDegrees,
            float intensity) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = localOrientation,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = SpotLightComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WriteSpotLightPayload(range, innerConeAngleDegrees, outerConeAngleDegrees, intensity)
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized directional-light entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localOrientation">Local orientation assigned to the entity.</param>
        /// <param name="intensity">Authored directional-light intensity.</param>
        /// <param name="shadowDistance">Authored directional-light shadow cutoff distance.</param>
        /// <returns>Serialized directional-light entity.</returns>
        SceneEntityAsset CreateDirectionalLightEntity(
            string id,
            string name,
            float3 localPosition,
            float4 localOrientation,
            float intensity,
            float shadowDistance) {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = localOrientation,
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = DirectionalLightComponentTypeId,
                        ComponentIndex = 0,
                        Payload = WriteDirectionalLightPayload(intensity, shadowDistance)
                    }
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one stable generated scene asset reference.
        /// </summary>
        /// <param name="relativePath">Virtual generated asset path.</param>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Stable generated scene asset reference.</returns>
        SceneAssetReference CreateGeneratedReference(string relativePath, string assetId) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = EngineProviderId,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Creates one stable file-backed material reference for the supplied PS2 basis-test material index.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>Scene asset reference targeting one file-backed colored material.</returns>
        SceneAssetReference CreatePs2BasisLightTestMaterialReference(int materialIndex) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = ResolvePs2BasisLightTestMaterialRelativePath(materialIndex),
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Writes the file-backed color materials used by the PS2 basis-light validation scene.
        /// </summary>
        /// <param name="assetsRootPath">Assets root path inside the target project.</param>
        void WritePs2BasisLightTestMaterialAssets(string assetsRootPath) {
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }

            string projectRootPath = Directory.GetParent(assetsRootPath)?.FullName
                ?? throw new InvalidOperationException("Project root path could not be resolved from the assets root.");
            for (int materialIndex = 0; materialIndex < 7; materialIndex++) {
                WritePs2BasisLightTestMaterialAsset(projectRootPath, materialIndex);
            }
        }

        /// <summary>
        /// Writes one file-backed color material and settings sidecar used by the PS2 basis-light validation scene.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        void WritePs2BasisLightTestMaterialAsset(string projectRootPath, int materialIndex) {
            string relativePath = ResolvePs2BasisLightTestMaterialRelativePath(materialIndex);
            string fullPath = Path.Combine(projectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException($"Could not resolve a material directory for '{relativePath}'.");
            }

            Directory.CreateDirectory(directoryPath);
            using (FileStream stream = File.Create(fullPath)) {
                global::helengine.editor.AssetSerializer.Serialize(stream, CreatePs2BasisLightTestMaterialAsset(materialIndex));
            }

            MaterialSettingsService.Save(fullPath, CreatePs2BasisLightTestMaterialSettings(materialIndex));
        }

        /// <summary>
        /// Creates one file-backed standard-shader material asset used by the PS2 basis-light validation scene.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>File-backed material asset for the supplied basis-test slot.</returns>
        MaterialAsset CreatePs2BasisLightTestMaterialAsset(int materialIndex) {
            return new MaterialAsset {
                Id = ResolvePs2BasisLightTestMaterialAssetId(materialIndex),
                ShaderAssetId = StandardShaderAssetId,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = MeshVariantName,
                RenderState = new MaterialRenderState(),
                ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>(),
                CastsShadows = true,
                ReceivesShadows = true
            };
        }

        /// <summary>
        /// Creates one per-platform settings sidecar for the supplied PS2 basis-test material.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>Generated import-settings payload for the supplied basis-test material.</returns>
        MaterialAssetImportSettings CreatePs2BasisLightTestMaterialSettings(int materialIndex) {
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = MaterialImporterId;
            settings.Importer.SourceChecksum = string.Empty;
            settings.Importer.AssetId = ResolvePs2BasisLightTestMaterialAssetId(materialIndex);

            string baseColor = ResolvePs2BasisLightTestMaterialColor(materialIndex);

            MaterialAssetProcessorSettings windowsSettings = new MaterialAssetProcessorSettings();
            windowsSettings.SchemaId = WindowsMaterialSchemaId;
            windowsSettings.FieldValues[UseCustomShaderFieldId] = "false";
            windowsSettings.FieldValues[TextureIdFieldId] = string.Empty;
            windowsSettings.FieldValues[CastsShadowFieldId] = "true";
            windowsSettings.FieldValues[ReceivesShadowFieldId] = "true";
            windowsSettings.FieldValues[BaseColorFieldId] = baseColor;
            settings.Processor.Platforms["windows"] = windowsSettings;

            MaterialAssetProcessorSettings ps2Settings = new MaterialAssetProcessorSettings();
            ps2Settings.SchemaId = Ps2MaterialSchemaId;
            ps2Settings.FieldValues[AlphaModeFieldId] = "opaque";
            ps2Settings.FieldValues[DoubleSidedFieldId] = "false";
            ps2Settings.FieldValues[Ps2CastShadowsFieldId] = "true";
            ps2Settings.FieldValues[VertexColorModeFieldId] = "ignore";
            ps2Settings.FieldValues[BaseColorFieldId] = baseColor;
            settings.Processor.Platforms["ps2"] = ps2Settings;
            return settings;
        }

        /// <summary>
        /// Resolves the stable project-relative material path for the supplied PS2 basis-test material index.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>Project-relative material path.</returns>
        string ResolvePs2BasisLightTestMaterialRelativePath(int materialIndex) {
            return "materials/rendering/ps2_basis_light_test/" + ResolvePs2BasisLightTestMaterialFileName(materialIndex);
        }

        /// <summary>
        /// Resolves the stable material asset id for the supplied PS2 basis-test material index.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>File-backed material asset identifier.</returns>
        string ResolvePs2BasisLightTestMaterialAssetId(int materialIndex) {
            return "Materials.rendering.ps2_basis_light_test." + Path.GetFileNameWithoutExtension(ResolvePs2BasisLightTestMaterialFileName(materialIndex));
        }

        /// <summary>
        /// Resolves the stable file name for the supplied PS2 basis-test material index.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>Material file name stored under the basis-test material folder.</returns>
        string ResolvePs2BasisLightTestMaterialFileName(int materialIndex) {
            switch (materialIndex) {
                case 0: return "Ground" + EditorFileTemplateRegistry.MaterialExtension;
                case 1: return "Center" + EditorFileTemplateRegistry.MaterialExtension;
                case 2: return "PlusX" + EditorFileTemplateRegistry.MaterialExtension;
                case 3: return "MinusX" + EditorFileTemplateRegistry.MaterialExtension;
                case 4: return "PlusZ" + EditorFileTemplateRegistry.MaterialExtension;
                case 5: return "MinusZ" + EditorFileTemplateRegistry.MaterialExtension;
                case 6: return "Corner" + EditorFileTemplateRegistry.MaterialExtension;
                default: throw new ArgumentOutOfRangeException(nameof(materialIndex), "Basis-test material index must be between 0 and 6.");
            }
        }

        /// <summary>
        /// Resolves the authored base color used by the supplied PS2 basis-test material index.
        /// </summary>
        /// <param name="materialIndex">Stable zero-based basis-test material index.</param>
        /// <returns>Hex RGBA color string stored in platform material settings.</returns>
        string ResolvePs2BasisLightTestMaterialColor(int materialIndex) {
            switch (materialIndex) {
                case 0: return "#202838FF";
                case 1: return "#F0F0F0FF";
                case 2: return "#FF4040FF";
                case 3: return "#C040FFFF";
                case 4: return "#40D060FF";
                case 5: return "#4080FFFF";
                case 6: return "#FFD040FF";
                default: throw new ArgumentOutOfRangeException(nameof(materialIndex), "Basis-test material index must be between 0 and 6.");
            }
        }

        /// <summary>
        /// Writes one serialized camera component payload.
        /// </summary>
        /// <returns>Serialized camera component payload.</returns>
        byte[] WriteCameraPayload() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(0));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(SceneObjectsLayerMask));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1280f, 720f)));
            writer.WriteField(
                "ClearSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(
                    fieldWriter,
                    new CameraClearSettings(
                        true,
                        new float4(0.06f, 0.06f, 0.09f, 1f),
                        true,
                        1f,
                        false,
                        0)));
            writer.WriteField(
                "RenderSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(
                    fieldWriter,
                    new CameraRenderSettings {
                        DepthPrepassMode = DepthPrepassMode.Auto,
                        ShadowDistance = 60f,
                        PostProcessTier = PostProcessTier.Disabled
                    }));
            return writer.BuildPayload();
        }

        /// <summary>
        /// Writes the camera payload used by the PS2 basis-light validation scene using the same contract as the known-good city rendering scenes.
        /// </summary>
        /// <returns>Serialized camera component payload.</returns>
        byte[] WritePs2BasisLightTestCameraPayload() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("CameraDrawOrder", fieldWriter => fieldWriter.WriteByte(0));
            writer.WriteField("LayerMask", fieldWriter => fieldWriter.WriteUInt16(SceneObjectsLayerMask));
            writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1f, 1f)));
            writer.WriteField("NearPlaneDistance", fieldWriter => fieldWriter.WriteSingle(0.1f));
            writer.WriteField("FarPlaneDistance", fieldWriter => fieldWriter.WriteSingle(64f));
            writer.WriteField(
                "ClearSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(
                    fieldWriter,
                    new CameraClearSettings(
                        true,
                        new float4(100f / 255f, 149f / 255f, 237f / 255f, 1f),
                        true,
                        1f,
                        false,
                        0)));
            writer.WriteField(
                "RenderSettings",
                fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(
                    fieldWriter,
                    new CameraRenderSettings {
                        DepthPrepassMode = DepthPrepassMode.Auto,
                        ShadowDistance = 24f,
                        PostProcessTier = PostProcessTier.Disabled
                    }));
            return writer.BuildPayload();
        }

        /// <summary>
        /// Writes one serialized mesh component payload.
        /// </summary>
        /// <param name="modelReference">Stable model reference used by the mesh.</param>
        /// <param name="materialReference">Stable material reference used by the mesh.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshPayload(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            } else if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            MeshComponent meshComponent = new MeshComponent {
                Model = PlaceholderModel,
                Material = PlaceholderMaterial,
                RenderOrder3D = 0
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(MeshModelReferenceName, modelReference);
            saveState.SetAssetReference(MeshMaterialReferenceName, materialReference);
            return MeshDescriptor.SerializeComponent(meshComponent, 0, saveState).Payload;
        }

        /// <summary>
        /// Writes one serialized point-light component payload.
        /// </summary>
        /// <param name="range">Authored point-light range.</param>
        /// <param name="intensity">Authored point-light intensity.</param>
        /// <returns>Serialized point-light component payload.</returns>
        byte[] WritePointLightPayload(float range, float intensity) {
            PointLightComponent lightComponent = new PointLightComponent {
                Color = new float4(1f, 0.92f, 0.78f, 1f),
                Intensity = intensity,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 1f,
                Range = range
            };
            return PointLightDescriptor.SerializeComponent(lightComponent, 0, null).Payload;
        }

        /// <summary>
        /// Writes one serialized spot-light component payload.
        /// </summary>
        /// <param name="range">Authored spot-light range.</param>
        /// <param name="innerConeAngleDegrees">Authored inner cone angle in degrees.</param>
        /// <param name="outerConeAngleDegrees">Authored outer cone angle in degrees.</param>
        /// <param name="intensity">Authored spot-light intensity.</param>
        /// <returns>Serialized spot-light component payload.</returns>
        byte[] WriteSpotLightPayload(float range, float innerConeAngleDegrees, float outerConeAngleDegrees, float intensity) {
            SpotLightComponent lightComponent = new SpotLightComponent {
                Color = new float4(1f, 0.93f, 0.82f, 1f),
                Intensity = intensity,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 1f,
                Range = range,
                InnerConeAngleDegrees = innerConeAngleDegrees,
                OuterConeAngleDegrees = outerConeAngleDegrees
            };
            return SpotLightDescriptor.SerializeComponent(lightComponent, 0, null).Payload;
        }

        /// <summary>
        /// Writes one serialized directional-light component payload.
        /// </summary>
        /// <param name="intensity">Authored directional-light intensity.</param>
        /// <param name="shadowDistance">Authored directional-light shadow cutoff distance.</param>
        /// <returns>Serialized directional-light component payload.</returns>
        byte[] WriteDirectionalLightPayload(float intensity, float shadowDistance) {
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(1f, 0.96f, 0.90f, 1f),
                Intensity = intensity,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 1f,
                ShadowDistance = shadowDistance
            };
            return DirectionalLightDescriptor.SerializeComponent(lightComponent, 0, null).Payload;
        }

        /// <summary>
        /// Writes one stable scene asset reference into the supplied payload writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the reference payload.</param>
        /// <param name="reference">Reference that should be serialized.</param>
        void WriteSceneAssetReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            writer.WriteByte(1);
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }

        /// <summary>
        /// Allocates the next scene-local entity id for the rendering scene currently being built.
        /// </summary>
        /// <returns>Next non-zero scene-local entity id.</returns>
        uint AllocateSceneEntityId() {
            return SceneEntityIdAllocator.Allocate();
        }
    }
}
