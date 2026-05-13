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
        /// Ensures directional and spot-light shader directions follow the shared engine light-direction convention.
        /// </summary>
        [Fact]
        public void Build_WhenDirectionalAndSpotLightsAreRotated_UsesSharedLightDirectionConvention() {
            InitializeCore();
            Entity directionalEntity = CreateLightEntity();
            float4 directionalOrientation;
            float4.CreateFromYawPitchRoll((float)(Math.PI * 0.5), 0f, 0f, out directionalOrientation);
            directionalEntity.LocalOrientation = directionalOrientation;
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalEntity.AddComponent(directionalLight);

            Entity spotEntity = CreateLightEntity();
            float4 spotOrientation;
            float4.CreateFromYawPitchRoll(0f, (float)(Math.PI * 0.5), 0f, out spotOrientation);
            spotEntity.LocalOrientation = spotOrientation;
            SpotLightComponent spotLight = new SpotLightComponent();
            spotEntity.AddComponent(spotLight);

            DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();

            DirectX11ForwardLightShaderData data = builder.Build([
                new RenderFrameLightSubmission(directionalLight, 10),
                new RenderFrameLightSubmission(spotLight, 9)
            ]);

            float3 expectedDirectionalDirection = LightDirectionUtility.GetLightDirection(directionalLight);
            float3 expectedSpotDirection = LightDirectionUtility.GetLightDirection(spotLight);

            Assert.Equal(expectedDirectionalDirection.X, data.Light0.DirectionAndShadow.X);
            Assert.Equal(expectedDirectionalDirection.Y, data.Light0.DirectionAndShadow.Y);
            Assert.Equal(expectedDirectionalDirection.Z, data.Light0.DirectionAndShadow.Z);
            Assert.Equal(expectedSpotDirection.X, data.Light1.DirectionAndShadow.X);
            Assert.Equal(expectedSpotDirection.Y, data.Light1.DirectionAndShadow.Y);
            Assert.Equal(expectedSpotDirection.Z, data.Light1.DirectionAndShadow.Z);
        }

        /// <summary>
        /// Ensures ambient lights accumulate into the ambient term without consuming packed direct-light slots.
        /// </summary>
        [Fact]
        public void Build_WhenAmbientLightsExist_AccumulatesAmbientColorWithoutConsumingDirectLightSlots() {
            InitializeCore();
            Entity firstAmbientEntity = CreateLightEntity();
            AmbientLightComponent firstAmbientLight = new AmbientLightComponent {
                Color = new float4(0.1f, 0.2f, 0.3f, 1f),
                Intensity = 2f
            };
            firstAmbientEntity.AddComponent(firstAmbientLight);

            Entity secondAmbientEntity = CreateLightEntity();
            AmbientLightComponent secondAmbientLight = new AmbientLightComponent {
                Color = new float4(0.2f, 0.1f, 0.05f, 1f),
                Intensity = 3f
            };
            secondAmbientEntity.AddComponent(secondAmbientLight);

            DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();

            DirectX11ForwardLightShaderData data = builder.Build([
                new RenderFrameLightSubmission(firstAmbientLight, 10),
                new RenderFrameLightSubmission(secondAmbientLight, 8)
            ]);

            Assert.Equal(0f, data.LightMetadata.X);
            Assert.InRange(data.AmbientLightColor.X, 0.7999f, 0.8001f);
            Assert.InRange(data.AmbientLightColor.Y, 0.6999f, 0.7001f);
            Assert.InRange(data.AmbientLightColor.Z, 0.7499f, 0.7501f);
        }

        /// <summary>
        /// Ensures ambient lights do not disturb the direct-light slot packing order when mixed with directional or local lights.
        /// </summary>
        [Fact]
        public void Build_WhenAmbientAndDirectLightsAreMixed_PacksOnlyDirectLightsIntoSlots() {
            InitializeCore();
            Entity directionalEntity = CreateLightEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent {
                ShadowsEnabled = false,
                Intensity = 2f
            };
            directionalEntity.AddComponent(directionalLight);

            Entity ambientEntity = CreateLightEntity();
            AmbientLightComponent ambientLight = new AmbientLightComponent {
                Color = new float4(0.25f, 0.1f, 0.05f, 1f),
                Intensity = 2f
            };
            ambientEntity.AddComponent(ambientLight);

            Entity pointEntity = CreateLightEntity();
            pointEntity.LocalPosition = new float3(4f, 2f, -1f);
            PointLightComponent pointLight = new PointLightComponent {
                Range = 9f,
                Intensity = 1.5f
            };
            pointEntity.AddComponent(pointLight);

            DirectX11ForwardLightShaderDataBuilder builder = new DirectX11ForwardLightShaderDataBuilder();

            DirectX11ForwardLightShaderData data = builder.Build([
                new RenderFrameLightSubmission(directionalLight, 10),
                new RenderFrameLightSubmission(ambientLight, 9),
                new RenderFrameLightSubmission(pointLight, 8)
            ]);

            Assert.Equal(2f, data.LightMetadata.X);
            Assert.Equal((float)LightType.Directional, data.Light0.ColorAndType.W);
            Assert.Equal((float)LightType.Point, data.Light1.ColorAndType.W);
            Assert.InRange(data.AmbientLightColor.X, 0.4999f, 0.5001f);
            Assert.InRange(data.AmbientLightColor.Y, 0.1999f, 0.2001f);
            Assert.InRange(data.AmbientLightColor.Z, 0.0999f, 0.1001f);
        }

        /// <summary>
        /// Initializes a minimal core instance so test entities can be created safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
