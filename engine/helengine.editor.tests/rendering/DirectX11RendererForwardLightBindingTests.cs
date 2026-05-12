using System.Runtime.CompilerServices;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the DirectX11 renderer prepares packed forward-light shader data from selected frame lights.
    /// </summary>
    public class DirectX11RendererForwardLightBindingTests {
        /// <summary>
        /// Ensures the renderer forwards the selected light set into the packed forward-light shader buffer path.
        /// </summary>
        [Fact]
        public void PrepareForwardLightState_WhenSelectedLightsExist_PacksSelectedLightsForUpload() {
            InitializeCore();
            Entity firstLightEntity = CreateLightEntity();
            DirectionalLightComponent firstLight = new DirectionalLightComponent();
            firstLight.ShadowsEnabled = false;
            firstLight.Intensity = 2f;
            firstLightEntity.AddComponent(firstLight);

            Entity secondLightEntity = CreateLightEntity();
            secondLightEntity.LocalPosition = new float3(5f, 0f, 1f);
            PointLightComponent secondLight = new PointLightComponent();
            secondLight.Range = 7f;
            secondLight.Intensity = 1.5f;
            secondLightEntity.AddComponent(secondLight);

            Entity ambientLightEntity = CreateLightEntity();
            AmbientLightComponent ambientLight = new AmbientLightComponent {
                Color = new float4(0.2f, 0.3f, 0.4f, 1f),
                Intensity = 2f
            };
            ambientLightEntity.AddComponent(ambientLight);

            CameraComponent camera = new CameraComponent();
            RenderFrame frame = new RenderFrame(
                camera,
                Array.Empty<RenderFrameDrawableSubmission>(),
                [
                    new RenderFrameLightSubmission(firstLight, 10),
                    new RenderFrameLightSubmission(ambientLight, 8),
                    new RenderFrameLightSubmission(secondLight, 5)
                ],
                Array.Empty<RenderFrameShadowCasterSubmission>());
            DirectX11RenderPassExecutionContext context = new DirectX11RenderPassExecutionContext(
                frame,
                new DirectX11SwapChainSurface(),
                [
                    new RenderFrameLightSubmission(firstLight, 10),
                    new RenderFrameLightSubmission(ambientLight, 8),
                    new RenderFrameLightSubmission(secondLight, 5)
                ]);
            ForwardLightCaptureRenderer renderer = ForwardLightCaptureRenderer.Create();

            renderer.PrepareForwardLightStateForTest(context);

            Assert.Equal(2f, renderer.LastForwardLightShaderData.LightMetadata.X);
            Assert.Equal((float)LightType.Directional, renderer.LastForwardLightShaderData.Light0.ColorAndType.W);
            Assert.Equal((float)LightType.Point, renderer.LastForwardLightShaderData.Light1.ColorAndType.W);
            Assert.Equal(7f, renderer.LastForwardLightShaderData.Light1.PositionAndRange.W);
            Assert.Equal(0.4f, renderer.LastForwardLightShaderData.AmbientLightColor.X);
            Assert.Equal(0.6f, renderer.LastForwardLightShaderData.AmbientLightColor.Y);
            Assert.Equal(0.8f, renderer.LastForwardLightShaderData.AmbientLightColor.Z);
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

        /// <summary>
        /// DirectX11-shaped renderer that captures packed forward-light shader data without touching GPU state.
        /// </summary>
        sealed class ForwardLightCaptureRenderer : TestDirectX11RenderManager3D {
            /// <summary>
            /// Gets the last packed forward-light shader data prepared by the renderer.
            /// </summary>
            public DirectX11ForwardLightShaderData LastForwardLightShaderData { get; private set; }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance.
            /// </summary>
            /// <returns>Renderer recorder that can prepare forward-light state without constructing DirectX11.</returns>
            public new static ForwardLightCaptureRenderer Create() {
                ForwardLightCaptureRenderer renderer = (ForwardLightCaptureRenderer)RuntimeHelpers.GetUninitializedObject(typeof(ForwardLightCaptureRenderer));
                renderer.LastForwardLightShaderData = new DirectX11ForwardLightShaderData();
                return renderer;
            }

            /// <summary>
            /// Exposes the protected forward-light preparation path for tests.
            /// </summary>
            /// <param name="context">Execution context whose selected lights should be packed.</param>
            public void PrepareForwardLightStateForTest(DirectX11RenderPassExecutionContext context) {
                PrepareForwardLightState(context);
            }

            /// <summary>
            /// Captures the packed shader data instead of uploading it to a real GPU buffer.
            /// </summary>
            /// <param name="data">Packed shader data prepared for the current selected light set.</param>
            protected override void UpdateForwardLightShaderData(DirectX11ForwardLightShaderData data) {
                LastForwardLightShaderData = data;
            }
        }
    }
}
