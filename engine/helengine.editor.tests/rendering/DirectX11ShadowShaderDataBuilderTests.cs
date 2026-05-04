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
        /// Initializes a core instance so cameras and entities can allocate engine-owned state safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
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
    }
}
