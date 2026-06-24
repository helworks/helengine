namespace helengine.editor {
    /// <summary>
    /// Creates exportable scene assets for physics validation and demo playback.
    /// </summary>
    public sealed class PhysicsValidationSceneFactory {
        /// <summary>
        /// Stable render order assigned to generated debug geometry meshes.
        /// </summary>
        const byte DefaultMeshRenderOrder = 0;

        /// <summary>
        /// Stable tagged field name used for mesh model-reference persistence.
        /// </summary>
        const string MeshModelReferenceFieldName = "Model";

        /// <summary>
        /// Stable tagged field name used for mesh material-reference array persistence.
        /// </summary>
        const string MeshMaterialReferencesFieldName = "Materials";

        /// <summary>
        /// Stable tagged field name used for mesh render-order persistence.
        /// </summary>
        const string MeshRenderOrder3DFieldName = "RenderOrder3D";

        /// <summary>
        /// Stable camera draw order assigned to validation-scene cameras.
        /// </summary>
        const byte DefaultCameraDrawOrder = 0;

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
        /// Relative project asset path for the shared physics demo mesh shader.
        /// </summary>
        const string PhysicsDemoShaderRelativePath = "Shaders/physics/PhysicsDemoMesh.hlsl";

        /// <summary>
        /// Relative project asset path for the neutral physics demo material.
        /// </summary>
        const string PhysicsDemoNeutralMaterialRelativePath = "Materials/physics/PhysicsDemoNeutral" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the non-shadow-casting ground physics demo material.
        /// </summary>
        const string PhysicsDemoGroundMaterialRelativePath = "Materials/physics/PhysicsDemoGround" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the blue physics demo material.
        /// </summary>
        const string PhysicsDemoBlueMaterialRelativePath = "Materials/physics/PhysicsDemoBlue" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the green physics demo material.
        /// </summary>
        const string PhysicsDemoGreenMaterialRelativePath = "Materials/physics/PhysicsDemoGreen" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the magenta physics demo material.
        /// </summary>
        const string PhysicsDemoMagentaMaterialRelativePath = "Materials/physics/PhysicsDemoMagenta" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the yellow physics demo material.
        /// </summary>
        const string PhysicsDemoYellowMaterialRelativePath = "Materials/physics/PhysicsDemoYellow" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the cyan physics demo material.
        /// </summary>
        const string PhysicsDemoCyanMaterialRelativePath = "Materials/physics/PhysicsDemoCyan" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the red physics demo material.
        /// </summary>
        const string PhysicsDemoRedMaterialRelativePath = "Materials/physics/PhysicsDemoRed" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the orange physics demo material.
        /// </summary>
        const string PhysicsDemoOrangeMaterialRelativePath = "Materials/physics/PhysicsDemoOrange" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Relative project asset path for the purple physics demo material.
        /// </summary>
        const string PhysicsDemoPurpleMaterialRelativePath = "Materials/physics/PhysicsDemoPurple" + EditorFileTemplateRegistry.MaterialExtension;

        /// <summary>
        /// Shader asset identifier derived from the shared physics demo shader path.
        /// </summary>
        const string PhysicsDemoShaderAssetId = "Shaders.physics.PhysicsDemoMesh";

        /// <summary>
        /// Vertex program name used by the shared physics demo shader.
        /// </summary>
        const string PhysicsDemoVertexProgramName = "PhysicsDemoMesh.vs";

        /// <summary>
        /// Pixel program name used by the shared physics demo shader.
        /// </summary>
        const string PhysicsDemoPixelProgramName = "PhysicsDemoMesh.ps";

        /// <summary>
        /// Shader variant name used by the shared physics demo materials.
        /// </summary>
        const string PhysicsDemoVariantName = "default";

        /// <summary>
        /// Material constant-buffer name consumed by the shared physics demo shader.
        /// </summary>
        const string MaterialColorBufferName = "MaterialColorBuffer";

        /// <summary>
        /// Material schema id used by the shared forward standard shader.
        /// </summary>
        const string StandardShaderSchemaId = "standard-shader";

        /// <summary>
        /// Material field id that disables custom shader overrides on standard materials.
        /// </summary>
        const string UseCustomShaderFieldId = "use-custom-shader";

        /// <summary>
        /// Material field id that stores the authored standard-shader base color.
        /// </summary>
        const string BaseColorFieldId = "base-color";

        /// <summary>
        /// Material field id that stores the authored diffuse texture id.
        /// </summary>
        const string TextureAssetIdFieldId = "texture-id";

        /// <summary>
        /// Material field id that controls shadow-map casting.
        /// </summary>
        const string CastsShadowFieldId = "casts-shadow";

        /// <summary>
        /// Material field id that controls shadow attenuation receiving.
        /// </summary>
        const string ReceivesShadowFieldId = "receives-shadow";

        /// <summary>
        /// Camera clear color used by physics validation scenes.
        /// </summary>
        static readonly float4 CornflowerBlueClearColor = new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f);

        /// <summary>
        /// Shared shader source used to render the exported physics demo meshes with per-material colors and shadowed forward lighting.
        /// </summary>
        const string PhysicsDemoShaderSource =
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 world;\n" +
            "    float4x4 worldViewProj;\n" +
            "    float4x4 normalMatrix;\n" +
            "    float4 cameraPosition;\n" +
            "};\n" +
            "\n" +
            "cbuffer ForwardLightBuffer : register(b1)\n" +
            "{\n" +
            "    float4 lightMetadata;\n" +
            "    float4 light0ColorAndType;\n" +
            "    float4 light0DirectionAndShadow;\n" +
            "    float4 light0PositionAndRange;\n" +
            "    float4 light0SpotAngles;\n" +
            "    float4 light1ColorAndType;\n" +
            "    float4 light1DirectionAndShadow;\n" +
            "    float4 light1PositionAndRange;\n" +
            "    float4 light1SpotAngles;\n" +
            "    float4 light2ColorAndType;\n" +
            "    float4 light2DirectionAndShadow;\n" +
            "    float4 light2PositionAndRange;\n" +
            "    float4 light2SpotAngles;\n" +
            "    float4 light3ColorAndType;\n" +
            "    float4 light3DirectionAndShadow;\n" +
            "    float4 light3PositionAndRange;\n" +
            "    float4 light3SpotAngles;\n" +
            "};\n" +
            "\n" +
            "cbuffer ShadowBuffer : register(b2)\n" +
            "{\n" +
            "    float4 shadowMetadata;\n" +
            "    float4 shadowLight0AtlasRect;\n" +
            "    float4 shadowLight0Metadata;\n" +
            "    float4x4 shadowLight0WorldToShadowClip;\n" +
            "    float4 shadowLight1AtlasRect;\n" +
            "    float4 shadowLight1Metadata;\n" +
            "    float4x4 shadowLight1WorldToShadowClip;\n" +
            "    float4 shadowLight2AtlasRect;\n" +
            "    float4 shadowLight2Metadata;\n" +
            "    float4x4 shadowLight2WorldToShadowClip;\n" +
            "    float4 shadowLight3AtlasRect;\n" +
            "    float4 shadowLight3Metadata;\n" +
            "    float4x4 shadowLight3WorldToShadowClip;\n" +
            "};\n" +
            "\n" +
            "cbuffer MaterialColorBuffer : register(b3)\n" +
            "{\n" +
            "    float4 surfaceColor;\n" +
            "};\n" +
            "\n" +
            "Texture2D shadowAtlasTexture : register(t1);\n" +
            "SamplerState shadowAtlasSampler : register(s1);\n" +
            "TextureCube pointShadowTexture0 : register(t2);\n" +
            "TextureCube pointShadowTexture1 : register(t3);\n" +
            "TextureCube pointShadowTexture2 : register(t4);\n" +
            "TextureCube pointShadowTexture3 : register(t5);\n" +
            "SamplerState pointShadowSampler : register(s2);\n" +
            "\n" +
            "struct VS_IN\n" +
            "{\n" +
            "    float3 pos : POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "struct PS_IN\n" +
            "{\n" +
            "    float4 pos : SV_POSITION;\n" +
            "    float3 worldPos : TEXCOORD0;\n" +
            "    float3 normal : TEXCOORD1;\n" +
            "};\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    float4 worldPosition = mul(float4(input.pos, 1.0f), world);\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.worldPos = worldPosition.xyz;\n" +
            "    output.normal = mul(float4(input.normal, 0.0f), normalMatrix).xyz;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float SamplePointShadowTexture(int textureIndex, float3 sampleDirection)\n" +
            "{\n" +
            "    if (textureIndex == 0)\n" +
            "    {\n" +
            "        return pointShadowTexture0.Sample(pointShadowSampler, sampleDirection).r;\n" +
            "    }\n" +
            "\n" +
            "    if (textureIndex == 1)\n" +
            "    {\n" +
            "        return pointShadowTexture1.Sample(pointShadowSampler, sampleDirection).r;\n" +
            "    }\n" +
            "\n" +
            "    if (textureIndex == 2)\n" +
            "    {\n" +
            "        return pointShadowTexture2.Sample(pointShadowSampler, sampleDirection).r;\n" +
            "    }\n" +
            "\n" +
            "    return pointShadowTexture3.Sample(pointShadowSampler, sampleDirection).r;\n" +
            "}\n" +
            "\n" +
            "float3 EvaluateForwardLight(\n" +
            "    float4 colorAndType,\n" +
            "    float4 directionAndShadow,\n" +
            "    float4 positionAndRange,\n" +
            "    float4 spotAngles,\n" +
            "    float4 shadowAtlasRect,\n" +
            "    float4 shadowSlotMetadata,\n" +
            "    float4x4 worldToShadowClip,\n" +
            "    float3 litSurfaceColor,\n" +
            "    float3 worldPos,\n" +
            "    float3 normal,\n" +
            "    float3 viewDirection)\n" +
            "{\n" +
            "    int lightType = (int)(colorAndType.w + 0.5f);\n" +
            "    float3 radiance = colorAndType.xyz;\n" +
            "    float3 lightDirection = float3(0.0f, 0.0f, 0.0f);\n" +
            "    float attenuation = 1.0f;\n" +
            "\n" +
            "    if (lightType == 0)\n" +
            "    {\n" +
            "        lightDirection = normalize(-directionAndShadow.xyz);\n" +
            "    }\n" +
            "    else\n" +
            "    {\n" +
            "        float3 toLight = positionAndRange.xyz - worldPos;\n" +
            "        float distanceToLight = length(toLight);\n" +
            "        if (distanceToLight <= 0.0001f || positionAndRange.w <= 0.0f)\n" +
            "        {\n" +
            "            return float3(0.0f, 0.0f, 0.0f);\n" +
            "        }\n" +
            "\n" +
            "        lightDirection = toLight / distanceToLight;\n" +
            "        float normalizedDistance = saturate(distanceToLight / positionAndRange.w);\n" +
            "        float rangeAttenuation = 1.0f - (normalizedDistance * normalizedDistance);\n" +
            "        attenuation = rangeAttenuation * rangeAttenuation;\n" +
            "\n" +
            "        if (lightType == 2)\n" +
            "        {\n" +
            "            float3 lightForward = normalize(directionAndShadow.xyz);\n" +
            "            float3 lightToSurface = normalize(worldPos - positionAndRange.xyz);\n" +
            "            float cone = dot(lightForward, lightToSurface);\n" +
            "            float coneRange = max(spotAngles.x - spotAngles.y, 0.0001f);\n" +
            "            float spotAttenuation = saturate((cone - spotAngles.y) / coneRange);\n" +
            "            attenuation *= spotAttenuation * spotAttenuation;\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    if (attenuation <= 0.0f)\n" +
            "    {\n" +
            "        return float3(0.0f, 0.0f, 0.0f);\n" +
            "    }\n" +
            "\n" +
            "    if (shadowSlotMetadata.x > 0.5f && shadowSlotMetadata.z < 1.5f && shadowMetadata.x > 0.5f)\n" +
            "    {\n" +
            "        float4 shadowClip = mul(float4(worldPos, 1.0f), worldToShadowClip);\n" +
            "        if (abs(shadowClip.w) > 0.0001f)\n" +
            "        {\n" +
            "            float3 shadowNdc = shadowClip.xyz / shadowClip.w;\n" +
            "            float2 shadowUv = float2((shadowNdc.x * 0.5f) + 0.5f, (-shadowNdc.y * 0.5f) + 0.5f);\n" +
            "            if (shadowUv.x >= 0.0f && shadowUv.x <= 1.0f && shadowUv.y >= 0.0f && shadowUv.y <= 1.0f && shadowNdc.z >= 0.0f && shadowNdc.z <= 1.0f)\n" +
            "            {\n" +
            "                float2 atlasUv = shadowAtlasRect.xy + (shadowUv * shadowAtlasRect.zw);\n" +
            "                float sampledDepth = shadowAtlasTexture.Sample(shadowAtlasSampler, atlasUv).r;\n" +
            "                float shadowBias = 0.0015f;\n" +
            "                float shadowVisibility = (shadowNdc.z - shadowBias) <= sampledDepth ? 1.0f : 0.0f;\n" +
            "                attenuation *= lerp(1.0f, shadowVisibility, shadowSlotMetadata.y);\n" +
            "            }\n" +
            "        }\n" +
            "    }\n" +
            "    else if (shadowSlotMetadata.x > 0.5f && shadowSlotMetadata.z > 1.5f && lightType == 1)\n" +
            "    {\n" +
            "        float3 lightToSurface = worldPos - positionAndRange.xyz;\n" +
            "        float distanceToSurface = length(lightToSurface);\n" +
            "        if (distanceToSurface > 0.0001f && positionAndRange.w > 0.0f)\n" +
            "        {\n" +
            "            int pointShadowTextureIndex = (int)(shadowSlotMetadata.w + 0.5f);\n" +
            "            float3 sampleDirection = lightToSurface / distanceToSurface;\n" +
            "            float currentDepth = saturate(distanceToSurface / positionAndRange.w);\n" +
            "            float sampledDepth = SamplePointShadowTexture(pointShadowTextureIndex, sampleDirection);\n" +
            "            float shadowBias = 0.01f;\n" +
            "            float shadowVisibility = (currentDepth - shadowBias) <= sampledDepth ? 1.0f : 0.0f;\n" +
            "            attenuation *= lerp(1.0f, shadowVisibility, shadowSlotMetadata.y);\n" +
            "        }\n" +
            "    }\n" +
            "\n" +
            "    float diffuse = saturate(dot(normal, lightDirection));\n" +
            "    if (diffuse <= 0.0f)\n" +
            "    {\n" +
            "        return float3(0.0f, 0.0f, 0.0f);\n" +
            "    }\n" +
            "\n" +
            "    float3 halfVector = normalize(lightDirection + viewDirection);\n" +
            "    float specular = pow(saturate(dot(normal, halfVector)), 32.0f);\n" +
            "    float3 diffuseColor = litSurfaceColor * radiance * diffuse * attenuation;\n" +
            "    float3 specularColor = radiance * specular * 0.35f * attenuation;\n" +
            "    return diffuseColor + specularColor;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    float3 ambientColor = float3(0.14f, 0.16f, 0.18f);\n" +
            "    float3 normal = normalize(input.normal);\n" +
            "    float3 viewDirection = normalize(cameraPosition.xyz - input.worldPos);\n" +
            "    float3 color = surfaceColor.xyz * ambientColor;\n" +
            "    int activeLightCount = (int)(lightMetadata.x + 0.5f);\n" +
            "\n" +
            "    if (activeLightCount > 0)\n" +
            "    {\n" +
            "        color += EvaluateForwardLight(light0ColorAndType, light0DirectionAndShadow, light0PositionAndRange, light0SpotAngles, shadowLight0AtlasRect, shadowLight0Metadata, shadowLight0WorldToShadowClip, surfaceColor.xyz, input.worldPos, normal, viewDirection);\n" +
            "    }\n" +
            "\n" +
            "    if (activeLightCount > 1)\n" +
            "    {\n" +
            "        color += EvaluateForwardLight(light1ColorAndType, light1DirectionAndShadow, light1PositionAndRange, light1SpotAngles, shadowLight1AtlasRect, shadowLight1Metadata, shadowLight1WorldToShadowClip, surfaceColor.xyz, input.worldPos, normal, viewDirection);\n" +
            "    }\n" +
            "\n" +
            "    if (activeLightCount > 2)\n" +
            "    {\n" +
            "        color += EvaluateForwardLight(light2ColorAndType, light2DirectionAndShadow, light2PositionAndRange, light2SpotAngles, shadowLight2AtlasRect, shadowLight2Metadata, shadowLight2WorldToShadowClip, surfaceColor.xyz, input.worldPos, normal, viewDirection);\n" +
            "    }\n" +
            "\n" +
            "    if (activeLightCount > 3)\n" +
            "    {\n" +
            "        color += EvaluateForwardLight(light3ColorAndType, light3DirectionAndShadow, light3PositionAndRange, light3SpotAngles, shadowLight3AtlasRect, shadowLight3Metadata, shadowLight3WorldToShadowClip, surfaceColor.xyz, input.worldPos, normal, viewDirection);\n" +
            "    }\n" +
            "\n" +
            "    return float4(saturate(color), surfaceColor.w);\n" +
            "}\n";

        /// <summary>
        /// Current payload version for serialized rigid-body component scene records.
        /// </summary>
        const byte RigidBodyComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized box-collider component scene records.
        /// </summary>
        const byte BoxColliderComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized sphere-collider component scene records.
        /// </summary>
        const byte SphereColliderComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized kinematic-motion component scene records.
        /// </summary>
        const byte KinematicMotionComponentPayloadVersion = 1;

        /// <summary>
        /// Current payload version for serialized character-controller component scene records.
        /// </summary>
        const byte CharacterControllerComponentPayloadVersion = 1;

        /// <summary>
        /// Serialized rigid-body kind byte for static bodies.
        /// </summary>
        const byte StaticBodyKindCode = 0;

        /// <summary>
        /// Serialized rigid-body kind byte for dynamic bodies.
        /// </summary>
        const byte DynamicBodyKindCode = 2;

        /// <summary>
        /// Serialized rigid-body kind byte for kinematic bodies.
        /// </summary>
        const byte KinematicBodyKindCode = 1;

        /// <summary>
        /// Allocates numeric entity ids while one validation scene asset is being built.
        /// </summary>
        readonly SceneEntityAssetIdAllocator SceneEntityIdAllocator;

        /// <summary>
        /// Initializes the validation-scene factory with a fresh scene-local entity id allocator.
        /// </summary>
        public PhysicsValidationSceneFactory() {
            SceneEntityIdAllocator = new SceneEntityAssetIdAllocator();
        }

        /// <summary>
        /// Creates one fully-authored physics validation scene asset for the requested scene id.
        /// </summary>
        /// <param name="sceneId">Stable relative scene id to author.</param>
        /// <returns>Generated scene asset ready for serialization.</returns>
        public SceneAsset CreateSceneAsset(string sceneId) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            SceneEntityIdAllocator.Reset();

            if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterSlopeSceneId, StringComparison.Ordinal)) {
                return CreateCharacterSlopeScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterStepsSceneId, StringComparison.Ordinal)) {
                return CreateCharacterStepsScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId, StringComparison.Ordinal)) {
                return CreateCharacterMovingPlatformScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, StringComparison.Ordinal)) {
                return CreateDynamicStackBoxesScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.DynamicSphereStackSceneId, StringComparison.Ordinal)) {
                return CreateDynamicSphereStackScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.DynamicMixedStackSceneId, StringComparison.Ordinal)) {
                return CreateDynamicMixedStackScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.KinematicPushSceneId, StringComparison.Ordinal)) {
                return CreateKinematicPushScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.MeshGroundStabilitySceneId, StringComparison.Ordinal)) {
                return CreateMeshGroundStabilityScene();
            } else if (string.Equals(sceneId, PhysicsValidationSceneCatalog.TriggerVolumeSceneId, StringComparison.Ordinal)) {
                return CreateTriggerVolumeScene();
            }

            throw new InvalidOperationException($"Unsupported physics validation scene id '{sceneId}'.");
        }

        /// <summary>
        /// Writes every known validation scene into the target project assets folder.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        public void WriteScenes(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            if (!Directory.Exists(assetsRootPath)) {
                throw new DirectoryNotFoundException($"Physics validation scene export requires an assets directory at '{assetsRootPath}'.");
            }

            WriteSupportAssets(projectRootPath);

            string[] sceneIds = PhysicsValidationSceneCatalog.GetSceneIds();
            for (int index = 0; index < sceneIds.Length; index++) {
                string sceneId = sceneIds[index];
                SceneAsset sceneAsset = CreateSceneAsset(sceneId);
                string fullPath = GetSceneFullPath(projectRootPath, sceneId);
                string directoryPath = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directoryPath)) {
                    throw new InvalidOperationException($"Could not resolve the directory path for scene '{sceneId}'.");
                }

                Directory.CreateDirectory(directoryPath);
                using FileStream stream = File.Create(fullPath);
                AssetSerializer.Serialize(stream, sceneAsset);
            }
        }

        /// <summary>
        /// Creates the character slope validation scene.
        /// </summary>
        /// <returns>Authored slope validation scene asset.</returns>
        SceneAsset CreateCharacterSlopeScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_slope.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("character_slope.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("character_slope.ramp", "SlopeRamp", new float3(2.25f, 0.6f, 0f), new float3(5f, 0.6f, 3f), CreateYawPitchRollDegrees(0.0, 0.0, 18.0), StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                    CreateCharacterControllerBoxMeshEntity("character_slope.controller", "CharacterController", new float3(-4f, 0.75f, 0f), new float3(0.9f, 1.5f, 0.9f), float4.Identity, new float3(1f, 0f, 0f), 3d, 1d, 0.75d, 0.3d, CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath)),
                    CreateMarkerEntity("character_slope.spawn", "ControllerSpawn", new float3(-4f, 0.75f, 0f)),
                    CreateMarkerEntity("character_slope.goal", "SlopeGoal", new float3(4.25f, 1.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_slope.camera", new float3(8.5f, 5f, 7.5f), CreateYawPitchRollDegrees(-135.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterSlopeSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the character steps validation scene.
        /// </summary>
        /// <returns>Authored steps validation scene asset.</returns>
        SceneAsset CreateCharacterStepsScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_steps.scenario",
                new[] {
                    CreateCubeMeshEntity("character_steps.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 12f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreateCubeMeshEntity("character_steps.step01", "Step01", new float3(0.75f, 0.15f, 0f), new float3(1.5f, 0.3f, 3f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoBlueMaterialRelativePath)),
                    CreateCubeMeshEntity("character_steps.step02", "Step02", new float3(2.25f, 0.45f, 0f), new float3(1.5f, 0.9f, 3f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                    CreateCubeMeshEntity("character_steps.step03", "Step03", new float3(3.75f, 0.75f, 0f), new float3(1.5f, 1.5f, 3f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                    CreateCubeMeshEntity("character_steps.step04", "Step04", new float3(5.25f, 1.05f, 0f), new float3(1.5f, 2.1f, 3f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath)),
                    CreateMarkerEntity("character_steps.spawn", "ControllerSpawn", new float3(-4.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_steps.camera", new float3(9f, 5.5f, 7f), CreateYawPitchRollDegrees(-138.0, -20.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterStepsSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the character moving-platform validation scene.
        /// </summary>
        /// <returns>Authored moving-platform validation scene asset.</returns>
        SceneAsset CreateCharacterMovingPlatformScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "character_moving_platform.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("character_moving_platform.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(18f, 1f, 14f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("character_moving_platform.gap_a", "GapEdgeA", new float3(-1.75f, 0.25f, 0f), new float3(4f, 0.5f, 4f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("character_moving_platform.gap_b", "GapEdgeB", new float3(4.75f, 0.25f, 0f), new float3(4f, 0.5f, 4f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                    CreateKinematicPhysicsBoxMeshEntity(
                        "character_moving_platform.platform",
                        "MovingPlatform",
                        new float3(-0.5f, 0.75f, 0f),
                        new float3(2.5f, 0.35f, 2.5f),
                        float4.Identity,
                        new float3(-0.5f, 0.75f, 0f),
                        new float3(3.5f, 0.75f, 0f),
                        2d,
                        true,
                        CreatePhysicsDemoMaterialReference(PhysicsDemoCyanMaterialRelativePath)),
                    CreateMarkerEntity("character_moving_platform.platform_start", "PlatformStart", new float3(-0.5f, 0.75f, 0f)),
                    CreateMarkerEntity("character_moving_platform.platform_end", "PlatformEnd", new float3(3.5f, 0.75f, 0f)),
                    CreateMarkerEntity("character_moving_platform.spawn", "ControllerSpawn", new float3(-5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("character_moving_platform.camera", new float3(10f, 5.75f, 8f), CreateYawPitchRollDegrees(-140.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.CharacterMovingPlatformSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the stacked dynamic-body validation scene.
        /// </summary>
        /// <returns>Authored stacked-box validation scene asset.</returns>
        SceneAsset CreateDynamicStackBoxesScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "dynamic_stack_boxes.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(14f, 1f, 14f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box01", "StackBox01", new float3(0f, 0.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoBlueMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box02", "StackBox02", new float3(0.5f, 1.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box03", "StackBox03", new float3(1.0f, 2.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("dynamic_stack_boxes.box04", "StackBox04", new float3(1.5f, 3.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                    CreateMarkerEntity("dynamic_stack_boxes.spawn", "DynamicSpawn", new float3(-2.5f, 1.5f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("dynamic_stack_boxes.camera", new float3(2.25f, 4.8f, 10.25f), CreateYawPitchRollDegrees(8.0, -16.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicStackBoxesSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the dynamic sphere-stack validation scene.
        /// </summary>
        /// <returns>Authored sphere-stack validation scene asset.</returns>
        SceneAsset CreateDynamicSphereStackScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "dynamic_sphere_stack.scenario",
                CreateDynamicSphereStackChildren());
            SceneEntityAsset cameraEntity = CreateCameraEntity("dynamic_sphere_stack.camera", new float3(9.5f, 6.75f, 9f), CreateYawPitchRollDegrees(45.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicSphereStackSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the static floor and dynamic sphere tower used by the sphere-stack validation scene.
        /// </summary>
        /// <returns>Scenario children containing a ground body, stacked spheres, and one spawn marker.</returns>
        SceneEntityAsset[] CreateDynamicSphereStackChildren() {
            List<SceneEntityAsset> children = new List<SceneEntityAsset>(10);
            children.Add(CreatePhysicsBoxMeshEntity("dynamic_sphere_stack.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 14f), float4.Identity, StaticBodyKindCode, false, CreateGeneratedStandardMaterialReference()));

            for (int sphereIndex = 0; sphereIndex < 8; sphereIndex++) {
                int sphereNumber = sphereIndex + 1;
                float staggerX = sphereIndex % 2 == 0 ? 0f : 0.08f;
                float staggerZ = sphereIndex % 3 == 0 ? -0.06f : 0.06f;
                children.Add(CreatePhysicsSphereMeshEntity(
                    "dynamic_sphere_stack.sphere" + sphereNumber.ToString("00"),
                    "StackSphere" + sphereNumber.ToString("00"),
                    new float3(staggerX, 0.5f + sphereIndex, staggerZ),
                    float4.Identity,
                    DynamicBodyKindCode,
                    true,
                    CreateSphereStackMaterialReference(sphereIndex)));
            }

            children.Add(CreateMarkerEntity("dynamic_sphere_stack.spawn", "SphereStackSpawn", new float3(0f, 0.5f, 0f)));
            return children.ToArray();
        }

        /// <summary>
        /// Creates the mixed dynamic box and sphere stack validation scene.
        /// </summary>
        /// <returns>Authored mixed primitive-stack validation scene asset.</returns>
        SceneAsset CreateDynamicMixedStackScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "dynamic_mixed_stack.scenario",
                CreateDynamicMixedStackChildren());
            SceneEntityAsset cameraEntity = CreateCameraEntity("dynamic_mixed_stack.camera", new float3(9.5f, 6.5f, 9f), CreateYawPitchRollDegrees(45.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.DynamicMixedStackSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates one alternating stack of dynamic cubes and spheres to expose box-sphere and sphere-box contacts in the same scene.
        /// </summary>
        /// <returns>Scenario children containing a ground body, mixed primitive stack, and one spawn marker.</returns>
        SceneEntityAsset[] CreateDynamicMixedStackChildren() {
            return new[] {
                CreatePhysicsBoxMeshEntity("dynamic_mixed_stack.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 14f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                CreatePhysicsBoxMeshEntity("dynamic_mixed_stack.box01", "StackBox01", new float3(0f, 0.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoBlueMaterialRelativePath)),
                CreatePhysicsSphereMeshEntity("dynamic_mixed_stack.sphere01", "StackSphere01", new float3(0.08f, 1.5f, -0.04f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                CreatePhysicsBoxMeshEntity("dynamic_mixed_stack.box02", "StackBox02", new float3(-0.06f, 2.5f, 0.05f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath)),
                CreatePhysicsSphereMeshEntity("dynamic_mixed_stack.sphere02", "StackSphere02", new float3(0.05f, 3.5f, 0.08f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                CreatePhysicsBoxMeshEntity("dynamic_mixed_stack.box03", "StackBox03", new float3(0.07f, 4.5f, -0.07f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoCyanMaterialRelativePath)),
                CreatePhysicsSphereMeshEntity("dynamic_mixed_stack.sphere03", "StackSphere03", new float3(-0.05f, 5.5f, 0.04f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoRedMaterialRelativePath)),
                CreatePhysicsBoxMeshEntity("dynamic_mixed_stack.box04", "StackBox04", new float3(0.03f, 6.5f, 0.06f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoOrangeMaterialRelativePath)),
                CreatePhysicsSphereMeshEntity("dynamic_mixed_stack.sphere04", "StackSphere04", new float3(-0.04f, 7.5f, -0.05f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoPurpleMaterialRelativePath)),
                CreateMarkerEntity("dynamic_mixed_stack.spawn", "MixedStackSpawn", new float3(0f, 0.5f, 0f))
            };
        }

        /// <summary>
        /// Creates the kinematic push validation scene.
        /// </summary>
        /// <returns>Authored kinematic push validation scene asset.</returns>
        SceneAsset CreateKinematicPushScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "kinematic_push.scenario",
                new[] {
                    CreatePhysicsBoxMeshEntity("kinematic_push.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(16f, 1f, 12f), float4.Identity, StaticBodyKindCode, false, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreatePhysicsBoxMeshEntity("kinematic_push.block", "DynamicTarget", new float3(1.5f, 0.5f, 0f), new float3(1f, 1f, 1f), float4.Identity, DynamicBodyKindCode, true, CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                    CreateKinematicPhysicsBoxMeshEntity(
                        "kinematic_push.pusher",
                        "KinematicPusher",
                        new float3(-2f, 0.5f, 0f),
                        new float3(1.5f, 1f, 1.5f),
                        float4.Identity,
                        new float3(-2f, 0.5f, 0f),
                        new float3(0.5f, 0.5f, 0f),
                        1d,
                        true,
                        CreatePhysicsDemoMaterialReference(PhysicsDemoCyanMaterialRelativePath)),
                    CreateMarkerEntity("kinematic_push.start", "PusherStart", new float3(-3.5f, 0.5f, 0f)),
                    CreateMarkerEntity("kinematic_push.end", "PusherEnd", new float3(0.5f, 0.5f, 0f)),
                    CreateMarkerEntity("kinematic_push.dynamic_spawn", "DynamicSpawn", new float3(1.5f, 0.5f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("kinematic_push.camera", new float3(8.5f, 4.75f, 7.25f), CreateYawPitchRollDegrees(-135.0, -16.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.KinematicPushSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the static-mesh ground stability validation scene.
        /// </summary>
        /// <returns>Authored static-ground stability validation scene asset.</returns>
        SceneAsset CreateMeshGroundStabilityScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "mesh_ground_stability.scenario",
                new[] {
                    CreateCubeMeshEntity("mesh_ground_stability.base", "GroundBase", new float3(0f, -0.5f, 0f), new float3(20f, 1f, 14f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreateCubeMeshEntity("mesh_ground_stability.section01", "StaticMeshGround01", new float3(-2.5f, 0.15f, 0f), new float3(3f, 0.3f, 4f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoBlueMaterialRelativePath)),
                    CreateCubeMeshEntity("mesh_ground_stability.section02", "StaticMeshGround02", new float3(0.5f, 0.35f, 0f), new float3(3f, 0.7f, 4f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath)),
                    CreateCubeMeshEntity("mesh_ground_stability.section03", "StaticMeshGround03", new float3(3.5f, 0.2f, 0f), new float3(3f, 0.4f, 4f), CreateYawPitchRollDegrees(0.0, 0.0, -6.0), CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath)),
                    CreateCubeMeshEntity("mesh_ground_stability.section04", "StaticMeshGround04", new float3(6.5f, 0.45f, 0f), new float3(3f, 0.9f, 4f), CreateYawPitchRollDegrees(0.0, 0.0, 5.0), CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath)),
                    CreateMarkerEntity("mesh_ground_stability.spawn", "WalkerSpawn", new float3(-5.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("mesh_ground_stability.camera", new float3(11f, 6f, 8.5f), CreateYawPitchRollDegrees(-140.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.MeshGroundStabilitySceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the trigger-volume validation scene.
        /// </summary>
        /// <returns>Authored trigger-volume validation scene asset.</returns>
        SceneAsset CreateTriggerVolumeScene() {
            SceneEntityAsset scenarioEntity = CreateScenarioRoot(
                "trigger_volume.scenario",
                new[] {
                    CreateCubeMeshEntity("trigger_volume.ground", "Ground", new float3(0f, -0.5f, 0f), new float3(18f, 1f, 12f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath)),
                    CreateCubeMeshEntity("trigger_volume.arch", "TriggerVolume", new float3(1.5f, 1.5f, 0f), new float3(2.5f, 3f, 2.5f), float4.Identity, CreatePhysicsDemoMaterialReference(PhysicsDemoCyanMaterialRelativePath)),
                    CreateMarkerEntity("trigger_volume.start", "PlayerPathStart", new float3(-5f, 0.75f, 0f)),
                    CreateMarkerEntity("trigger_volume.end", "PlayerPathEnd", new float3(5.5f, 0.75f, 0f))
                });
            SceneEntityAsset cameraEntity = CreateCameraEntity("trigger_volume.camera", new float3(9.5f, 5f, 7.5f), CreateYawPitchRollDegrees(-136.0, -18.0, 0.0));
            return CreateSceneAsset(PhysicsValidationSceneCatalog.TriggerVolumeSceneId, cameraEntity, scenarioEntity);
        }

        /// <summary>
        /// Creates the final scene asset wrapper shared by every validation scenario.
        /// </summary>
        /// <param name="sceneId">Stable relative scene id.</param>
        /// <param name="cameraEntity">Root camera entity.</param>
        /// <param name="scenarioEntity">Root scenario entity.</param>
        /// <returns>Scene asset ready for serialization.</returns>
        SceneAsset CreateSceneAsset(
            string sceneId,
            SceneEntityAsset cameraEntity,
            SceneEntityAsset scenarioEntity) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }
            if (cameraEntity == null) {
                throw new ArgumentNullException(nameof(cameraEntity));
            }
            if (scenarioEntity == null) {
                throw new ArgumentNullException(nameof(scenarioEntity));
            }

            return new SceneAsset {
                Id = sceneId,
                AssetReferences = CreateAssetReferences(),
                RootEntities = new[] { cameraEntity, scenarioEntity }
            };
        }

        /// <summary>
        /// Creates the scenario root entity that owns the authored test geometry and markers.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="children">Authored scenario children.</param>
        /// <returns>Scenario root entity.</returns>
        SceneEntityAsset CreateScenarioRoot(string entityId, SceneEntityAsset[] children) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Scenario entity id must be provided.", nameof(entityId));
            }
            if (children == null) {
                throw new ArgumentNullException(nameof(children));
            }

            SceneEntityAsset[] sceneChildren = AppendKeyLight(children);

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "Scenario",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = sceneChildren
            };
        }

        /// <summary>
        /// Creates one camera root entity for a validation scene.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="position">Camera position.</param>
        /// <param name="orientation">Camera orientation.</param>
        /// <returns>Camera entity with a serialized camera component.</returns>
        SceneEntityAsset CreateCameraEntity(string entityId, float3 position, float4 orientation) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Camera entity id must be provided.", nameof(entityId));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "Camera",
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = orientation,
                Components = new[] { CreateCameraComponentRecord() },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed cube entity for the validation scene.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <returns>Mesh-backed entity.</returns>
        SceneEntityAsset CreateCubeMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            SceneAssetReference materialReference) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Mesh entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Mesh entity name must be provided.", nameof(name));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] { CreateMeshComponentRecord(materialReference) },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that also carries serialized 3D physics records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="bodyKindCode">Rigid-body participation mode byte to serialize.</param>
        /// <param name="useGravity">True when the serialized rigid body should receive gravity.</param>
        /// <returns>Mesh-backed entity with serialized rigid-body and box-collider records.</returns>
        SceneEntityAsset CreatePhysicsBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            byte bodyKindCode,
            bool useGravity,
            SceneAssetReference materialReference) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Physics entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Physics entity name must be provided.", nameof(name));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(materialReference),
                    CreateRigidBodyComponentRecord(bodyKindCode, useGravity, 1d, 1d, float3.Zero, 1),
                    CreateBoxColliderComponentRecord(scale, 2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed sphere entity that also carries serialized 3D physics records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="bodyKindCode">Rigid-body participation mode byte to serialize.</param>
        /// <param name="useGravity">True when the serialized rigid body should receive gravity.</param>
        /// <param name="materialReference">Material reference used by the visible sphere mesh.</param>
        /// <returns>Mesh-backed entity with serialized rigid-body and sphere-collider records.</returns>
        SceneEntityAsset CreatePhysicsSphereMeshEntity(
            string entityId,
            string name,
            float3 position,
            float4 orientation,
            byte bodyKindCode,
            bool useGravity,
            SceneAssetReference materialReference) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Physics sphere entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Physics sphere entity name must be provided.", nameof(name));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel(), materialReference),
                    CreateRigidBodyComponentRecord(bodyKindCode, useGravity, 1d, 1d, float3.Zero, 1),
                    CreateSphereColliderComponentRecord(0.5f, 2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that also carries serialized 3D kinematic-motion records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="startLocalPosition">Kinematic motion start position.</param>
        /// <param name="endLocalPosition">Kinematic motion end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the motion should reverse at the end.</param>
        /// <returns>Mesh-backed entity with serialized rigid-body, box-collider, and kinematic-motion records.</returns>
        SceneEntityAsset CreateKinematicPhysicsBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong,
            SceneAssetReference materialReference) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Physics entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Physics entity name must be provided.", nameof(name));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(materialReference),
                    CreateRigidBodyComponentRecord(KinematicBodyKindCode, false, 1d, 1d, float3.Zero, 1),
                    CreateBoxColliderComponentRecord(scale, 2),
                    CreateKinematicMotionComponentRecord(startLocalPosition, endLocalPosition, travelDurationSeconds, pingPong, 3)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one mesh-backed box entity that carries serialized 3D character-controller records.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="scale">Entity scale and collider size.</param>
        /// <param name="orientation">Entity orientation.</param>
        /// <param name="desiredMoveDirection">Desired local move direction used by the controller.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <returns>Mesh-backed entity with serialized box-collider and character-controller records.</returns>
        SceneEntityAsset CreateCharacterControllerBoxMeshEntity(
            string entityId,
            string name,
            float3 position,
            float3 scale,
            float4 orientation,
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance,
            SceneAssetReference materialReference) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Character controller entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Character controller entity name must be provided.", nameof(name));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation,
                Components = new[] {
                    CreateMeshComponentRecord(materialReference),
                    CreateBoxColliderComponentRecord(scale, 1),
                    CreateCharacterControllerComponentRecord(desiredMoveDirection, moveSpeed, gravityScale, stepHeight, groundSnapDistance, 2)
                },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one empty marker entity used as a future spawn, target, or motion reference.
        /// </summary>
        /// <param name="entityId">Stable serialized entity id.</param>
        /// <param name="name">Authored entity name.</param>
        /// <param name="position">Marker position.</param>
        /// <returns>Marker entity without components.</returns>
        SceneEntityAsset CreateMarkerEntity(string entityId, string name, float3 position) {
            if (string.IsNullOrWhiteSpace(entityId)) {
                throw new ArgumentException("Marker entity id must be provided.", nameof(entityId));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Marker entity name must be provided.", nameof(name));
            }

            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = name,
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates the shared generated-asset reference list used by validation scene mesh components.
        /// </summary>
        /// <returns>Stable generated asset reference list.</returns>
        static SceneAssetReference[] CreateAssetReferences() {
            return new[] {
                global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel(),
                global::helengine.EngineSceneAssetReferenceFactory.CreateSphereModel(),
                CreateGeneratedStandardMaterialReference(),
                CreatePhysicsDemoMaterialReference(PhysicsDemoGroundMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoNeutralMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoBlueMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoGreenMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoMagentaMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoYellowMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoCyanMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoRedMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoOrangeMaterialRelativePath),
                CreatePhysicsDemoMaterialReference(PhysicsDemoPurpleMaterialRelativePath)
            };
        }

        /// <summary>
        /// Creates the file-backed material reference assigned to one dynamic sphere in the sphere-stack validation scene.
        /// </summary>
        /// <param name="sphereIndex">Zero-based sphere index.</param>
        /// <returns>Distinct colored material reference for the requested sphere.</returns>
        static SceneAssetReference CreateSphereStackMaterialReference(int sphereIndex) {
            if (sphereIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(sphereIndex), "Sphere index must be non-negative.");
            }

            string[] materialPaths = {
                PhysicsDemoBlueMaterialRelativePath,
                PhysicsDemoGreenMaterialRelativePath,
                PhysicsDemoMagentaMaterialRelativePath,
                PhysicsDemoYellowMaterialRelativePath,
                PhysicsDemoCyanMaterialRelativePath,
                PhysicsDemoRedMaterialRelativePath,
                PhysicsDemoOrangeMaterialRelativePath,
                PhysicsDemoPurpleMaterialRelativePath
            };
            return CreatePhysicsDemoMaterialReference(materialPaths[sphereIndex % materialPaths.Length]);
        }

        /// <summary>
        /// Creates one file-backed scene asset reference used for the exported physics demo materials.
        /// </summary>
        /// <param name="relativePath">Relative project asset path.</param>
        /// <returns>Scene asset reference targeting a file-backed asset.</returns>
        static SceneAssetReference CreatePhysicsDemoMaterialReference(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemMaterial(relativePath);
        }

        /// <summary>
        /// Creates one generated reference to the platform-owned standard material.
        /// </summary>
        /// <returns>Generated standard material scene asset reference.</returns>
        static SceneAssetReference CreateGeneratedStandardMaterialReference() {
            return global::helengine.EngineSceneAssetReferenceFactory.CreateStandardMaterial();
        }

        /// <summary>
        /// Creates one serialized mesh component record that references the generated cube model and standard material.
        /// </summary>
        /// <returns>Serialized mesh component record.</returns>
        static SceneComponentAssetRecord CreateMeshComponentRecord(SceneAssetReference materialReference) {
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            SceneAssetReference modelReference = global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel();

            return CreateMeshComponentRecord(modelReference, materialReference);
        }

        /// <summary>
        /// Creates one serialized mesh component record that references the supplied generated model and material.
        /// </summary>
        /// <param name="modelReference">Generated model reference serialized into the mesh component payload.</param>
        /// <param name="materialReference">Material reference serialized into the mesh component payload.</param>
        /// <returns>Serialized mesh component record.</returns>
        static SceneComponentAssetRecord CreateMeshComponentRecord(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            if (modelReference == null) {
                throw new ArgumentNullException(nameof(modelReference));
            }
            if (materialReference == null) {
                throw new ArgumentNullException(nameof(materialReference));
            }

            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(MeshModelReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, modelReference));
            writer.WriteField(MeshMaterialReferencesFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReferenceArray(fieldWriter, new[] { materialReference }));
            writer.WriteField(MeshRenderOrder3DFieldName, fieldWriter => fieldWriter.WriteByte(DefaultMeshRenderOrder));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.MeshComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Creates one serialized camera component record using the editor scene-object layer mask.
        /// </summary>
        /// <returns>Serialized camera component record.</returns>
        static SceneComponentAssetRecord CreateCameraComponentRecord() {
            CameraClearSettings clearSettings = new CameraClearSettings(true, CornflowerBlueClearColor, true, 1f, false, 0);
            CameraRenderSettings renderSettings = new CameraRenderSettings {
                DepthPrepassMode = DepthPrepassMode.Disabled,
                ShadowDistance = 0f,
                PostProcessTier = PostProcessTier.Disabled
            };
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField(CameraDrawOrderFieldName, fieldWriter => fieldWriter.WriteByte(DefaultCameraDrawOrder));
            writer.WriteField(CameraLayerMaskFieldName, fieldWriter => fieldWriter.WriteUInt16(EditorLayerMasks.SceneObjects));
            writer.WriteField(CameraViewportFieldName, fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1f, 1f)));
            writer.WriteField(CameraNearPlaneDistanceFieldName, fieldWriter => fieldWriter.WriteSingle(0.1f));
            writer.WriteField(CameraFarPlaneDistanceFieldName, fieldWriter => fieldWriter.WriteSingle(100f));
            writer.WriteField(CameraClearSettingsFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraClearSettings(fieldWriter, clearSettings));
            writer.WriteField(CameraRenderSettingsFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(fieldWriter, renderSettings));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CameraComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };
        }

        /// <summary>
        /// Appends the shared key light to the authored scenario children.
        /// </summary>
        /// <param name="children">Authored scenario children.</param>
        /// <returns>Copied child array with the shared key light appended at the end.</returns>
        SceneEntityAsset[] AppendKeyLight(SceneEntityAsset[] children) {
            if (children == null) {
                throw new ArgumentNullException(nameof(children));
            }

            SceneEntityAsset[] sceneChildren = new SceneEntityAsset[children.Length + 1];
            Array.Copy(children, sceneChildren, children.Length);
            sceneChildren[children.Length] = CreateKeyLightEntity();
            return sceneChildren;
        }

        /// <summary>
        /// Creates the shared directional light used to give the exported validation scenes stronger shape and visible shadows.
        /// </summary>
        /// <returns>Directional light entity appended to each scenario root.</returns>
        SceneEntityAsset CreateKeyLightEntity() {
            return new SceneEntityAsset {
                Id = AllocateSceneEntityId(),
                Name = "KeyLight",
                LocalPosition = new float3(0f, 6f, 0f),
                LocalScale = float3.One,
                LocalOrientation = CreateYawPitchRollDegrees(-48.0, -44.0, 0.0),
                Components = new[] { CreateDirectionalLightComponentRecord() },
                Children = Array.Empty<SceneEntityAsset>()
            };
        }

        /// <summary>
        /// Creates one serialized directional light component record configured for shadowed validation-scene rendering.
        /// </summary>
        /// <returns>Directional light scene component payload.</returns>
        static SceneComponentAssetRecord CreateDirectionalLightComponentRecord() {
            DirectionalLightComponent lightComponent = new DirectionalLightComponent {
                Color = new float4(1.0f, 0.96f, 0.90f, 1.0f),
                Intensity = 1f,
                ShadowsEnabled = true,
                ShadowMapMode = ShadowMapMode.Forced,
                ShadowStrength = 0.95f
            };
            return CreateAutomaticComponentRecord(lightComponent, 0);
        }

        /// <summary>
        /// Writes the shared shader and material assets consumed by the exported physics validation scenes.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        static void WriteSupportAssets(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            DeleteObsoletePhysicsDemoShaderAsset(projectRootPath);
            WriteMaterialAsset(projectRootPath, PhysicsDemoGroundMaterialRelativePath, "PhysicsDemoGround", new float4(0.77f, 0.80f, 0.84f, 1.0f), false, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoNeutralMaterialRelativePath, "PhysicsDemoNeutral", new float4(0.77f, 0.80f, 0.84f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoBlueMaterialRelativePath, "PhysicsDemoBlue", new float4(0.33f, 0.56f, 0.90f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoGreenMaterialRelativePath, "PhysicsDemoGreen", new float4(0.38f, 0.76f, 0.49f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoMagentaMaterialRelativePath, "PhysicsDemoMagenta", new float4(0.84f, 0.42f, 0.73f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoYellowMaterialRelativePath, "PhysicsDemoYellow", new float4(0.92f, 0.79f, 0.33f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoCyanMaterialRelativePath, "PhysicsDemoCyan", new float4(0.31f, 0.79f, 0.82f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoRedMaterialRelativePath, "PhysicsDemoRed", new float4(0.90f, 0.32f, 0.29f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoOrangeMaterialRelativePath, "PhysicsDemoOrange", new float4(0.95f, 0.52f, 0.22f, 1.0f), true, true);
            WriteMaterialAsset(projectRootPath, PhysicsDemoPurpleMaterialRelativePath, "PhysicsDemoPurple", new float4(0.55f, 0.43f, 0.92f, 1.0f), true, true);
        }

        /// <summary>
        /// Deletes the obsolete custom shader generated by older physics demo material exports.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        static void DeleteObsoletePhysicsDemoShaderAsset(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullPath = Path.Combine(projectRootPath, "assets", PhysicsDemoShaderRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) {
                File.Delete(fullPath);
            }
        }

        /// <summary>
        /// Writes the shared file-backed HLSL shader used by the exported physics validation scenes.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        static void WriteShaderAsset(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullPath = Path.Combine(projectRootPath, "assets", PhysicsDemoShaderRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException($"Could not resolve a directory path for shader '{PhysicsDemoShaderRelativePath}'.");
            }

            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(fullPath, PhysicsDemoShaderSource);
        }

        /// <summary>
        /// Writes one file-backed material asset used by the exported physics validation scenes.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path that owns the `assets` directory.</param>
        /// <param name="relativePath">Relative project asset path for the material file.</param>
        /// <param name="assetId">Serialized material asset identifier.</param>
        /// <param name="surfaceColor">Authored standard material base color.</param>
        static void WriteMaterialAsset(string projectRootPath, string relativePath, string assetId, float4 surfaceColor, bool castsShadows, bool receivesShadows) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            GeneratedMaterialAssetDefinition definition = new GeneratedMaterialAssetDefinition {
                MaterialAsset = new MaterialAsset {
                    Id = assetId,
                    RenderState = new MaterialRenderState(),
                    CastsShadows = castsShadows,
                    ReceivesShadows = receivesShadows
                }
            };

            IReadOnlyList<string> supportedPlatforms = new EditorProjectPlatformsService(projectRootPath).Load().SupportedPlatforms;
            for (int platformIndex = 0; platformIndex < supportedPlatforms.Count; platformIndex++) {
                GeneratedMaterialPlatformDefinition platformDefinition = definition.GetOrCreatePlatform(supportedPlatforms[platformIndex]);
                platformDefinition.SchemaId = StandardShaderSchemaId;
                platformDefinition.SetFieldValue(UseCustomShaderFieldId, "false");
                platformDefinition.SetFieldValue(TextureAssetIdFieldId, string.Empty);
                platformDefinition.SetFieldValue(CastsShadowFieldId, castsShadows ? "true" : "false");
                platformDefinition.SetFieldValue(ReceivesShadowFieldId, receivesShadows ? "true" : "false");
                platformDefinition.SetFieldValue(BaseColorFieldId, ConvertColorToHtml(surfaceColor));
            }

            GeneratedMaterialAssetWriteService writeService = new GeneratedMaterialAssetWriteService();
            writeService.WriteMaterial(projectRootPath, relativePath, definition);
        }

        /// <summary>
        /// Converts one normalized color into the material settings HTML color format.
        /// </summary>
        /// <param name="color">Normalized color value to serialize.</param>
        /// <returns>HTML color string in #RRGGBBAA format.</returns>
        static string ConvertColorToHtml(float4 color) {
            return string.Concat(
                "#",
                ConvertColorChannelToByte(color.X).ToString("X2"),
                ConvertColorChannelToByte(color.Y).ToString("X2"),
                ConvertColorChannelToByte(color.Z).ToString("X2"),
                ConvertColorChannelToByte(color.W).ToString("X2"));
        }

        /// <summary>
        /// Converts one normalized color channel into an 8-bit channel value.
        /// </summary>
        /// <param name="value">Normalized channel value.</param>
        /// <returns>Clamped byte channel.</returns>
        static byte ConvertColorChannelToByte(float value) {
            double scaledValue = Math.Round(Math.Clamp((double)value, 0d, 1d) * 255d, MidpointRounding.AwayFromZero);
            return (byte)scaledValue;
        }

        /// <summary>
        /// Creates one serialized rigid-body component record.
        /// </summary>
        /// <param name="bodyKindCode">Rigid-body participation mode byte to serialize.</param>
        /// <param name="useGravity">True when gravity should be enabled.</param>
        /// <param name="mass">Serialized authored mass value.</param>
        /// <param name="gravityScale">Serialized authored gravity scale.</param>
        /// <param name="linearVelocity">Serialized authored linear velocity.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized rigid-body component record.</returns>
        static SceneComponentAssetRecord CreateRigidBodyComponentRecord(
            byte bodyKindCode,
            bool useGravity,
            double mass,
            double gravityScale,
            float3 linearVelocity,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            RigidBody3DComponent component = new RigidBody3DComponent {
                AngularVelocity = float3.Zero,
                BodyKind = ResolveBodyKind(bodyKindCode),
                GravityScale = gravityScale,
                LinearVelocity = linearVelocity,
                Mass = mass,
                UseGravity = useGravity
            };
            return CreateAutomaticComponentRecord(component, componentIndex);
        }

        /// <summary>
        /// Creates one serialized box-collider component record.
        /// </summary>
        /// <param name="size">Serialized authored full collider size.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized box-collider component record.</returns>
        static SceneComponentAssetRecord CreateBoxColliderComponentRecord(float3 size, int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            BoxCollider3DComponent component = new BoxCollider3DComponent {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                DynamicFriction = 0.4d,
                IsTrigger = false,
                Restitution = 0d,
                Size = size,
                StaticFriction = 0.6d
            };
            return CreateAutomaticComponentRecord(component, componentIndex);
        }

        /// <summary>
        /// Creates one serialized sphere-collider component record.
        /// </summary>
        /// <param name="radius">Serialized authored sphere radius.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized sphere-collider component record.</returns>
        static SceneComponentAssetRecord CreateSphereColliderComponentRecord(float radius, int componentIndex) {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere radius must be a finite value greater than zero.");
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            SphereCollider3DComponent component = new SphereCollider3DComponent {
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue,
                DynamicFriction = 0.4d,
                IsTrigger = false,
                Radius = radius,
                Restitution = 0d,
                StaticFriction = 0.6d
            };
            return CreateAutomaticComponentRecord(component, componentIndex);
        }

        /// <summary>
        /// Serializes one generated component through the same reflected editor payload path used by authored scene components.
        /// </summary>
        /// <param name="component">Component instance to serialize.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized scene component record.</returns>
        static SceneComponentAssetRecord CreateAutomaticComponentRecord(Component component, int componentIndex) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }

            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            return descriptor.SerializeComponent(component, componentIndex, null);
        }

        /// <summary>
        /// Converts the compact generated body-kind code into the runtime enum used by reflected persistence.
        /// </summary>
        /// <param name="bodyKindCode">Serialized generated body-kind code.</param>
        /// <returns>Runtime body-kind enum value.</returns>
        static BodyKind3D ResolveBodyKind(byte bodyKindCode) {
            if (bodyKindCode == StaticBodyKindCode) {
                return BodyKind3D.Static;
            }
            if (bodyKindCode == KinematicBodyKindCode) {
                return BodyKind3D.Kinematic;
            }
            if (bodyKindCode == DynamicBodyKindCode) {
                return BodyKind3D.Dynamic;
            }

            throw new ArgumentOutOfRangeException(nameof(bodyKindCode), "Unsupported generated rigid-body kind code.");
        }

        /// <summary>
        /// Creates one serialized kinematic-motion component record.
        /// </summary>
        /// <param name="startLocalPosition">Motion path start position.</param>
        /// <param name="endLocalPosition">Motion path end position.</param>
        /// <param name="travelDurationSeconds">One-way travel duration in seconds.</param>
        /// <param name="pingPong">True when the motion should reverse at the end.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized kinematic-motion component record.</returns>
        static SceneComponentAssetRecord CreateKinematicMotionComponentRecord(
            float3 startLocalPosition,
            float3 endLocalPosition,
            double travelDurationSeconds,
            bool pingPong,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (double.IsNaN(travelDurationSeconds) || double.IsInfinity(travelDurationSeconds) || travelDurationSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(travelDurationSeconds), "Travel duration must be a finite value greater than zero.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(KinematicMotionComponentPayloadVersion);
            writer.WriteFloat3(startLocalPosition);
            writer.WriteFloat3(endLocalPosition);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(travelDurationSeconds));
            writer.WriteByte(pingPong ? (byte)1 : (byte)0);

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.KinematicMotion3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Creates one serialized character-controller component record.
        /// </summary>
        /// <param name="desiredMoveDirection">Desired planar move direction.</param>
        /// <param name="moveSpeed">Horizontal move speed in world units per second.</param>
        /// <param name="gravityScale">Gravity multiplier used by the controller.</param>
        /// <param name="stepHeight">Maximum upward snap height used while climbing support surfaces.</param>
        /// <param name="groundSnapDistance">Maximum downward snap distance used to keep the controller grounded.</param>
        /// <param name="componentIndex">Entity-local component order index.</param>
        /// <returns>Serialized character-controller component record.</returns>
        static SceneComponentAssetRecord CreateCharacterControllerComponentRecord(
            float3 desiredMoveDirection,
            double moveSpeed,
            double gravityScale,
            double stepHeight,
            double groundSnapDistance,
            int componentIndex) {
            if (componentIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be non-negative.");
            }
            if (double.IsNaN(moveSpeed) || double.IsInfinity(moveSpeed) || moveSpeed < 0d) {
                throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Move speed must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(gravityScale) || double.IsInfinity(gravityScale) || gravityScale < 0d) {
                throw new ArgumentOutOfRangeException(nameof(gravityScale), "Gravity scale must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(stepHeight) || double.IsInfinity(stepHeight) || stepHeight < 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepHeight), "Step height must be a finite value greater than or equal to zero.");
            }
            if (double.IsNaN(groundSnapDistance) || double.IsInfinity(groundSnapDistance) || groundSnapDistance < 0d) {
                throw new ArgumentOutOfRangeException(nameof(groundSnapDistance), "Ground snap distance must be a finite value greater than or equal to zero.");
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(CharacterControllerComponentPayloadVersion);
            writer.WriteFloat3(desiredMoveDirection);
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(moveSpeed));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(gravityScale));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(stepHeight));
            writer.WriteInt64(BitConverter.DoubleToInt64Bits(groundSnapDistance));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CharacterController3DComponent",
                ComponentIndex = componentIndex,
                Payload = stream.ToArray()
            };
        }

        /// <summary>
        /// Writes one optional scene asset reference into a component payload.
        /// </summary>
        /// <param name="writer">Destination writer receiving the payload.</param>
        /// <param name="reference">Reference to serialize.</param>
        static void WriteOptionalReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            writer.WriteByte(1);
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
        }

        /// <summary>
        /// Writes one `float4` payload into a binary component stream.
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
        /// Creates one quaternion from yaw, pitch, and roll angles expressed in degrees.
        /// </summary>
        /// <param name="yawDegrees">Yaw around the Y axis in degrees.</param>
        /// <param name="pitchDegrees">Pitch around the X axis in degrees.</param>
        /// <param name="rollDegrees">Roll around the Z axis in degrees.</param>
        /// <returns>Converted quaternion.</returns>
        static float4 CreateYawPitchRollDegrees(double yawDegrees, double pitchDegrees, double rollDegrees) {
            float4.CreateFromYawPitchRoll(
                (float)(yawDegrees * Math.PI / 180.0),
                (float)(pitchDegrees * Math.PI / 180.0),
                (float)(rollDegrees * Math.PI / 180.0),
                out float4 result);
            return result;
        }

        /// <summary>
        /// Resolves the absolute output path for one relative physics validation scene id.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <param name="sceneId">Relative scene id stored in the asset.</param>
        /// <returns>Absolute output file path.</returns>
        static string GetSceneFullPath(string projectRootPath, string sceneId) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string relativePath = sceneId.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRootPath, "assets", relativePath);
        }

        /// <summary>
        /// Allocates the next scene-local entity id for the validation scene currently being built.
        /// </summary>
        /// <returns>Next non-zero scene-local entity id.</returns>
        uint AllocateSceneEntityId() {
            return SceneEntityIdAllocator.Allocate();
        }
    }
}
