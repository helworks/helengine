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
        /// Initializes the committed rendering scene writer with the persistence descriptors required for authored editor-scene output.
        /// </summary>
        public RenderingSceneWriter() {
            MeshDescriptor = new MeshComponentPersistenceDescriptor();
            PointLightDescriptor = new PointLightComponentPersistenceDescriptor();
            SpotLightDescriptor = new SpotLightComponentPersistenceDescriptor();
            DirectionalLightDescriptor = new DirectionalLightComponentPersistenceDescriptor();
            PlaceholderModel = new AuthoringPlaceholderRuntimeModel();
            PlaceholderMaterial = new RuntimeMaterial();
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

            WritePointShadowSceneAsset(assetsRootPath);
            WritePointShadowLabSceneAsset(assetsRootPath);
            WriteSpotShadowLabSceneAsset(assetsRootPath);
            WriteDirectionalShadowLabSceneAsset(assetsRootPath);
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
                CreateGeneratedReference("Engine/Materials/Standard", StandardMaterialAssetId)
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
        /// Creates one serialized camera entity for the smoke scene.
        /// </summary>
        /// <param name="id">Stable entity id.</param>
        /// <param name="name">Display name stored on the entity.</param>
        /// <param name="localPosition">Local position assigned to the entity.</param>
        /// <param name="localOrientation">Local orientation assigned to the entity.</param>
        /// <returns>Serialized camera entity.</returns>
        SceneEntityAsset CreateCameraEntity(string id, string name, float3 localPosition, float4 localOrientation) {
            return new SceneEntityAsset {
                Id = id,
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
                Id = id,
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
                Id = id,
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
                Id = id,
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
                Id = id,
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
    }
}
