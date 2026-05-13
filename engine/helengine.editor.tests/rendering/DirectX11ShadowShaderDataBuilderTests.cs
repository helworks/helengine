using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies packing of atlas-shadow shader data for the built-in DirectX11 forward shader.
    /// </summary>
    public class DirectX11ShadowShaderDataBuilderTests {
        /// <summary>
        /// Ensures atlas shadow slots align with the selected forward-light order and skip point lights without atlas allocations.
        /// </summary>
        [Fact]
        public void Build_WhenAtlasShadowLightsExist_PacksMatchingSelectedLightSlots() {
            InitializeCore();
            CameraComponent camera = CreateCamera();

            Entity directionalEntity = CreateEntity(new float3(0f, 0f, 0f));
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalEntity.AddComponent(directionalLight);

            Entity pointEntity = CreateEntity(new float3(2f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointEntity.AddComponent(pointLight);

            Entity spotEntity = CreateEntity(new float3(1f, 2f, 3f));
            SpotLightComponent spotLight = new SpotLightComponent();
            float4 spotOrientation;
            float4.CreateFromYawPitchRoll((float)Math.PI, 0f, 0f, out spotOrientation);
            spotEntity.LocalOrientation = spotOrientation;
            spotEntity.AddComponent(spotLight);

            RenderFrameLightSubmission[] selectedLights = [
                new RenderFrameLightSubmission(directionalLight, 10),
                new RenderFrameLightSubmission(pointLight, 9),
                new RenderFrameLightSubmission(spotLight, 8)
            ];
            DirectX11ShadowResourceSet shadowResourceSet = new DirectX11ShadowResourceSet(
                [selectedLights[0], selectedLights[2]],
                [
                    new DirectX11ShadowAtlasAllocation(selectedLights[0], 0, 0, 1024, 1024),
                    new DirectX11ShadowAtlasAllocation(selectedLights[2], 1024, 0, 1024, 1024)
                ],
                Array.Empty<DirectX11PointShadowResource>(),
                2048,
                2048);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            DirectX11ShadowShaderData data = builder.Build(camera, selectedLights, shadowResourceSet);

            Assert.Equal(1f, data.ShadowMetadata.X);
            Assert.Equal(2f, data.ShadowMetadata.W);
            Assert.Equal(1f, data.Light0Metadata.X);
            Assert.Equal(0f, data.Light1Metadata.X);
            Assert.Equal(1f, data.Light2Metadata.X);
            Assert.Equal(0.5f, data.Light2AtlasRect.Z);
            Assert.Equal(0.5f, data.Light2AtlasRect.W);
        }

        /// <summary>
        /// Ensures empty shadow resources disable all packed shadow slots.
        /// </summary>
        [Fact]
        public void Build_WhenNoAtlasShadowLightsExist_ReturnsDisabledShadowSlots() {
            InitializeCore();
            CameraComponent camera = CreateCamera();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            DirectX11ShadowShaderData data = builder.Build(
                camera,
                [new RenderFrameLightSubmission(pointLight, 5)],
                new DirectX11ShadowResourceSet(
                    Array.Empty<RenderFrameLightSubmission>(),
                    Array.Empty<DirectX11ShadowAtlasAllocation>(),
                    Array.Empty<DirectX11PointShadowResource>(),
                    0,
                    0));

            Assert.Equal(0f, data.ShadowMetadata.X);
            Assert.Equal(0f, data.Light0Metadata.X);
        }

        /// <summary>
        /// Ensures point-shadow resources pack cube-shadow metadata into the selected light slot.
        /// </summary>
        [Fact]
        public void Build_WhenPointShadowResourcesExist_PacksCubeShadowMetadata() {
            InitializeCore();
            CameraComponent camera = CreateCamera();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointLight.Range = 12f;
            pointLight.ShadowStrength = 0.35f;
            pointEntity.AddComponent(pointLight);
            RenderFrameLightSubmission pointSubmission = new RenderFrameLightSubmission(pointLight, 10);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            DirectX11ShadowShaderData data = builder.Build(
                camera,
                [pointSubmission],
                new DirectX11ShadowResourceSet(
                    [pointSubmission],
                    Array.Empty<DirectX11ShadowAtlasAllocation>(),
                    [new DirectX11PointShadowResource(pointSubmission, 512)],
                    0,
                    0));

            Assert.Equal(0f, data.ShadowMetadata.X);
            Assert.Equal(1f, data.Light0Metadata.X);
            Assert.Equal(0.35f, data.Light0Metadata.Y);
            Assert.Equal(2f, data.Light0Metadata.Z);
            Assert.Equal(0f, data.Light0Metadata.W);
        }

        /// <summary>
        /// Ensures directional shadow projection size follows the authored light shadow distance instead of the camera shadow distance.
        /// </summary>
        [Fact]
        public void BuildShadowViewProjectionMatrix_WhenDirectionalLightOverridesCameraDistance_UsesLightShadowDistance() {
            InitializeCore();
            CameraComponent camera = CreateCamera();
            camera.RenderSettings.ShadowDistance = 10f;

            Entity directionalEntity = CreateEntity(new float3(0f, 0f, 0f));
            DirectionalLightComponent directionalLight = new DirectionalLightComponent {
                ShadowDistance = 80f
            };
            directionalEntity.AddComponent(directionalLight);
            RenderFrameLightSubmission directionalSubmission = new RenderFrameLightSubmission(directionalLight, 10);
            DirectX11ShadowAtlasAllocation allocation = new DirectX11ShadowAtlasAllocation(directionalSubmission, 0, 0, 1024, 1024);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 lightViewProjection = builder.BuildShadowViewProjectionMatrix(camera, allocation);
            float3 clipPoint = TransformPointToNormalizedDeviceCoordinates(new float3(30f, 0f, 5f), lightViewProjection);

            Assert.InRange(Math.Abs(clipPoint.X), 0f, 1f);
        }

        /// <summary>
        /// Ensures large directional shadow distances still cover geometry close to the camera instead of pushing the whole shadow box too far ahead.
        /// </summary>
        [Fact]
        public void BuildShadowViewProjectionMatrix_WhenDirectionalShadowDistanceIsLarge_KeepsNearCameraGeometryInsideTheShadowVolume() {
            InitializeCore();

            Entity cameraEntity = CreateEntity(float3.Zero);
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);

            Entity directionalEntity = CreateEntity(float3.Zero);
            DirectionalLightComponent directionalLight = new DirectionalLightComponent {
                ShadowDistance = 200f
            };
            directionalEntity.AddComponent(directionalLight);
            RenderFrameLightSubmission directionalSubmission = new RenderFrameLightSubmission(directionalLight, 10);
            DirectX11ShadowAtlasAllocation allocation = new DirectX11ShadowAtlasAllocation(directionalSubmission, 0, 0, 1024, 1024);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 lightViewProjection = builder.BuildShadowViewProjectionMatrix(camera, allocation);
            float3 nearCameraPointClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0f, -10f), lightViewProjection);
            float3 distantPointClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0f, -150f), lightViewProjection);

            Assert.InRange(nearCameraPointClip.X, -1f, 1f);
            Assert.InRange(nearCameraPointClip.Y, -1f, 1f);
            Assert.InRange(nearCameraPointClip.Z, 0f, 1f);
            Assert.InRange(distantPointClip.X, -1f, 1f);
            Assert.InRange(distantPointClip.Y, -1f, 1f);
            Assert.InRange(distantPointClip.Z, 0f, 1f);
        }

        /// <summary>
        /// Ensures directional shadow targeting follows the camera view ahead of the camera instead of centering the shadow box on the camera position itself.
        /// </summary>
        [Fact]
        public void BuildShadowViewProjectionMatrix_WhenCameraLooksTowardDistantSceneCenter_KeepsVisibleSceneAheadInsideDirectionalShadowVolume() {
            InitializeCore();

            Entity cameraEntity = CreateEntity(new float3(0.136f, 24f, 67.999863f));
            float4 cameraOrientation;
            float4.CreateFromYawPitchRoll(0f, -0.32f, 0f, out cameraOrientation);
            cameraEntity.LocalOrientation = cameraOrientation;
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);

            Entity directionalEntity = CreateEntity(float3.Zero);
            float4 directionalOrientation;
            float4.CreateFromYawPitchRoll(0f, -0.95f, 0f, out directionalOrientation);
            directionalEntity.LocalOrientation = directionalOrientation;
            DirectionalLightComponent directionalLight = new DirectionalLightComponent {
                ShadowDistance = 60f
            };
            directionalEntity.AddComponent(directionalLight);
            RenderFrameLightSubmission directionalSubmission = new RenderFrameLightSubmission(directionalLight, 10);
            DirectX11ShadowAtlasAllocation allocation = new DirectX11ShadowAtlasAllocation(directionalSubmission, 0, 0, 1024, 1024);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 lightViewProjection = builder.BuildShadowViewProjectionMatrix(camera, allocation);
            float3 clipPoint = TransformPointToNormalizedDeviceCoordinates(float3.Zero, lightViewProjection);

            Assert.InRange(clipPoint.X, -1f, 1f);
            Assert.InRange(clipPoint.Y, -1f, 1f);
            Assert.InRange(clipPoint.Z, 0f, 1f);
        }

        /// <summary>
        /// Ensures the point-shadow cube Z faces align with the engine forward convention instead of mirroring across the light.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingZFaces_MapsNegativeAndPositiveZToTheirExpectedCubeFaces() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 positiveZFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 4);
            float4x4 negativeZFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 5);
            float3 negativeZPointClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0f, -1f), positiveZFaceMatrix);
            float3 positiveZPointClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0f, 1f), negativeZFaceMatrix);

            Assert.InRange(Math.Abs(negativeZPointClip.X), 0f, 0.0001f);
            Assert.InRange(Math.Abs(negativeZPointClip.Y), 0f, 0.0001f);
            Assert.InRange(Math.Abs(positiveZPointClip.X), 0f, 0.0001f);
            Assert.InRange(Math.Abs(positiveZPointClip.Y), 0f, 0.0001f);
        }

        /// <summary>
        /// Ensures the positive X cube face maps world positive Z to the left and negative Z to the right according to the Direct3D cubemap convention.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingPositiveXFace_PreservesExpectedHorizontalOrientation() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 positiveXFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 0);
            float3 negativeZOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(1f, 0f, -0.25f), positiveXFaceMatrix);
            float3 positiveZOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(1f, 0f, 0.25f), positiveXFaceMatrix);

            Assert.True(negativeZOffsetClip.X > 0f);
            Assert.True(positiveZOffsetClip.X < 0f);
        }

        /// <summary>
        /// Ensures the positive Y cube face maps world positive X to the right and negative X to the left according to the Direct3D cubemap convention.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingPositiveYFace_PreservesExpectedHorizontalOrientation() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 positiveYFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 2);
            float3 negativeXOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(-0.25f, 1f, 0f), positiveYFaceMatrix);
            float3 positiveXOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(0.25f, 1f, 0f), positiveYFaceMatrix);

            Assert.True(negativeXOffsetClip.X < 0f);
            Assert.True(positiveXOffsetClip.X > 0f);
        }

        /// <summary>
        /// Ensures the negative X cube face maps world positive Z to the right and negative Z to the left according to the Direct3D cubemap convention.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingNegativeXFace_PreservesExpectedHorizontalOrientation() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 negativeXFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 1);
            float3 negativeZOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(-1f, 0f, -0.25f), negativeXFaceMatrix);
            float3 positiveZOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(-1f, 0f, 0.25f), negativeXFaceMatrix);

            Assert.True(negativeZOffsetClip.X < 0f);
            Assert.True(positiveZOffsetClip.X > 0f);
        }

        /// <summary>
        /// Ensures the positive Z cube face maps world positive Y upward and negative Y downward according to the Direct3D cubemap convention.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingPositiveZFace_PreservesExpectedVerticalOrientation() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 positiveZFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 4);
            float3 negativeYOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, -0.25f, 1f), positiveZFaceMatrix);
            float3 positiveYOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0.25f, 1f), positiveZFaceMatrix);

            Assert.True(negativeYOffsetClip.Y < 0f);
            Assert.True(positiveYOffsetClip.Y > 0f);
        }

        /// <summary>
        /// Ensures the negative Z cube face maps world positive Y upward and negative Y downward according to the Direct3D cubemap convention.
        /// </summary>
        [Fact]
        public void BuildPointShadowViewProjectionMatrix_WhenUsingNegativeZFace_PreservesExpectedVerticalOrientation() {
            InitializeCore();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 12f;
            pointEntity.AddComponent(pointLight);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 negativeZFaceMatrix = builder.BuildPointShadowViewProjectionMatrix(pointLight, 5);
            float3 negativeYOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, -0.25f, -1f), negativeZFaceMatrix);
            float3 positiveYOffsetClip = TransformPointToNormalizedDeviceCoordinates(new float3(0f, 0.25f, -1f), negativeZFaceMatrix);

            Assert.True(negativeYOffsetClip.Y < 0f);
            Assert.True(positiveYOffsetClip.Y > 0f);
        }

        /// <summary>
        /// Ensures spot-light shadow projection treats the authored outer cone angle as a half-angle, matching the forward-light shading path.
        /// </summary>
        [Fact]
        public void BuildShadowViewProjectionMatrix_WhenSpotLightUsesOuterConeHalfAngle_CoversPointsInsideTheAuthoredCone() {
            InitializeCore();
            CameraComponent camera = CreateCamera();

            Entity spotEntity = CreateEntity(float3.Zero);
            SpotLightComponent spotLight = new SpotLightComponent {
                Range = 20f,
                OuterConeAngleDegrees = 35f
            };
            spotEntity.AddComponent(spotLight);

            RenderFrameLightSubmission spotSubmission = new RenderFrameLightSubmission(spotLight, 10);
            DirectX11ShadowAtlasAllocation allocation = new DirectX11ShadowAtlasAllocation(spotSubmission, 0, 0, 1024, 1024);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 lightViewProjection = builder.BuildShadowViewProjectionMatrix(camera, allocation);
            double outerConeRadians = spotLight.OuterConeAngleDegrees * (Math.PI / 180.0);
            float3 coneEdgePoint = new float3((float)(Math.Tan(outerConeRadians) * 10.0), 0f, -10f);
            float3 clipPoint = TransformPointToNormalizedDeviceCoordinates(coneEdgePoint, lightViewProjection);

            Assert.InRange(clipPoint.X, 0.95f, 1.05f);
            Assert.InRange(clipPoint.Y, -0.05f, 0.05f);
            Assert.InRange(clipPoint.Z, 0f, 1f);
        }

        /// <summary>
        /// Ensures spotlight shadow projection adds enough depth margin that receivers at the end of the authored range stay inside the shadow frustum instead of landing exactly on the far clip plane.
        /// </summary>
        [Fact]
        public void BuildShadowViewProjectionMatrix_WhenSpotLightReceiverSitsAtAuthoredRange_KeepsReceiverInsideShadowFrustum() {
            InitializeCore();
            CameraComponent camera = CreateCamera();

            Entity spotEntity = CreateEntity(new float3(0f, 16f, 0f));
            float3 rotationAxis = new float3(1f, 0f, 0f);
            float4 spotOrientation;
            float4.CreateFromAxisAngle(ref rotationAxis, (float)(-Math.PI * 0.5), out spotOrientation);
            spotEntity.LocalOrientation = spotOrientation;

            SpotLightComponent spotLight = new SpotLightComponent {
                Range = 16f,
                OuterConeAngleDegrees = 45f
            };
            spotEntity.AddComponent(spotLight);

            float3 lightDirection = LightDirectionUtility.GetEntityForwardDirection(spotEntity);
            float3 receiverPoint = spotEntity.Position + (lightDirection * spotLight.Range);

            RenderFrameLightSubmission spotSubmission = new RenderFrameLightSubmission(spotLight, 10);
            DirectX11ShadowAtlasAllocation allocation = new DirectX11ShadowAtlasAllocation(spotSubmission, 0, 0, 1024, 1024);
            DirectX11ShadowShaderDataBuilder builder = new DirectX11ShadowShaderDataBuilder();

            float4x4 lightViewProjection = builder.BuildShadowViewProjectionMatrix(camera, allocation);
            float3 clipPoint = TransformPointToNormalizedDeviceCoordinates(receiverPoint, lightViewProjection);

            Assert.InRange(receiverPoint.Y, -0.01f, 0.01f);
            Assert.True(clipPoint.Z < 1f, "Spotlight receivers at the authored range should remain inside the shadow frustum.");
        }

        /// <summary>
        /// Initializes a core instance so cameras and entities can allocate engine-owned state safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Creates a camera attached to an initialized entity.
        /// </summary>
        /// <returns>Camera ready for shadow-projection calculations.</returns>
        CameraComponent CreateCamera() {
            Entity cameraEntity = CreateEntity(new float3(0f, 0f, 5f));
            CameraComponent camera = new CameraComponent();
            cameraEntity.AddComponent(camera);
            return camera;
        }

        /// <summary>
        /// Creates an initialized entity at the requested position.
        /// </summary>
        /// <param name="position">Position assigned to the entity.</param>
        /// <returns>Initialized entity.</returns>
        Entity CreateEntity(float3 position) {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.LocalPosition = position;
            return entity;
        }

        /// <summary>
        /// Transforms one world-space point by a row-major world-view-projection matrix and returns normalized-device coordinates.
        /// </summary>
        /// <param name="point">World-space point to transform.</param>
        /// <param name="matrix">Row-major matrix used by the runtime shader path.</param>
        /// <returns>Normalized-device coordinates for the transformed point.</returns>
        float3 TransformPointToNormalizedDeviceCoordinates(float3 point, float4x4 matrix) {
            float clipX = (point.X * matrix.M11) + (point.Y * matrix.M21) + (point.Z * matrix.M31) + matrix.M41;
            float clipY = (point.X * matrix.M12) + (point.Y * matrix.M22) + (point.Z * matrix.M32) + matrix.M42;
            float clipZ = (point.X * matrix.M13) + (point.Y * matrix.M23) + (point.Z * matrix.M33) + matrix.M43;
            float clipW = (point.X * matrix.M14) + (point.Y * matrix.M24) + (point.Z * matrix.M34) + matrix.M44;
            if (Math.Abs(clipW) <= 0.0001f) {
                throw new InvalidOperationException("Point-shadow clip-space transform produced an invalid W component.");
            }

            return new float3(clipX / clipW, clipY / clipW, clipZ / clipW);
        }
    }
}
