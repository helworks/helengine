using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies packed DirectX11 forward-light shader data built from selected frame lights.
    /// </summary>
    public class DirectX11ForwardLightShaderDataBuilderTests {
        /// <summary>
        /// Ensures directional, point, and spot lights are packed with stable type, color, direction, range, and cone data.
        /// </summary>
        [Fact]
        public void Build_WhenDirectionalPointAndSpotLightsExist_PacksExpectedLightFields() {
            InitializeCore();
            Entity directionalEntity = CreateLightEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalLight.ShadowsEnabled = false;
            directionalLight.Color = new float4(1f, 0.5f, 0.25f, 1f);
            directionalLight.Intensity = 2f;
            directionalEntity.AddComponent(directionalLight);

            Entity pointEntity = CreateLightEntity();
            pointEntity.LocalPosition = new float3(4f, 2f, -1f);
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.Range = 9f;
            pointLight.Intensity = 1.5f;
            pointEntity.AddComponent(pointLight);

            Entity spotEntity = CreateLightEntity();
            spotEntity.LocalPosition = new float3(-3f, 1f, 6f);
            SpotLightComponent spotLight = new SpotLightComponent();
            spotLight.Range = 14f;
            spotLight.InnerConeAngleDegrees = 20f;
            spotLight.OuterConeAngleDegrees = 35f;
            spotLight.Intensity = 3f;
            spotEntity.AddComponent(spotLight);

            DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();

            DirectX11ForwardLightShaderData data = builder.Build([
                new RenderFrameLightSubmission(directionalLight, 10),
                new RenderFrameLightSubmission(pointLight, 8),
                new RenderFrameLightSubmission(spotLight, 6)
            ]);

            Assert.Equal(3f, data.LightMetadata.X);
            Assert.Equal((float)LightType.Directional, data.Light0.ColorAndType.W);
            Assert.Equal(2f, data.Light0.ColorAndType.X);
            Assert.Equal(1f, data.Light0.ColorAndType.Y);
            Assert.Equal(0.5f, data.Light0.ColorAndType.Z);
            Assert.Equal(-1f, data.Light0.DirectionAndShadow.Z);
            Assert.Equal((float)LightType.Point, data.Light1.ColorAndType.W);
            Assert.Equal(4f, data.Light1.PositionAndRange.X);
            Assert.Equal(2f, data.Light1.PositionAndRange.Y);
            Assert.Equal(-1f, data.Light1.PositionAndRange.Z);
            Assert.Equal(9f, data.Light1.PositionAndRange.W);
            Assert.Equal((float)LightType.Spot, data.Light2.ColorAndType.W);
            Assert.Equal(-3f, data.Light2.PositionAndRange.X);
            Assert.Equal(1f, data.Light2.PositionAndRange.Y);
            Assert.Equal(6f, data.Light2.PositionAndRange.Z);
            Assert.Equal(14f, data.Light2.PositionAndRange.W);
            Assert.True(data.Light2.SpotAngles.X > data.Light2.SpotAngles.Y);
        }

        /// <summary>
        /// Ensures the packed forward-light buffer clamps to the built-in shader light-slot budget.
        /// </summary>
        [Fact]
        public void Build_WhenSelectedLightsExceedPackedBudget_ClampsToMaximumShaderLightCount() {
            InitializeCore();
            DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();
            List<RenderFrameLightSubmission> selectedLights = new List<RenderFrameLightSubmission>();
            for (int index = 0; index < 6; index++) {
                Entity entity = CreateLightEntity();
                DirectionalLightComponent light = new DirectionalLightComponent();
                light.ShadowsEnabled = false;
                light.Intensity = index + 1;
                entity.AddComponent(light);
                selectedLights.Add(new RenderFrameLightSubmission(light, index));
            }

            DirectX11ForwardLightShaderData data = builder.Build(selectedLights);

            Assert.Equal(DirectX11ForwardLightShaderDataBuilder.MaximumPackedLightCount, data.LightMetadata.X);
        }

        /// <summary>
        /// Initializes a minimal core instance so test entities can be created safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates one light-owning entity with initialized component storage.
        /// </summary>
        /// <returns>Entity ready to receive authored light components.</returns>
        Entity CreateLightEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            return entity;
        }
    }
}
