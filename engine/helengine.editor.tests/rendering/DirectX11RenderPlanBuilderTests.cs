using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 forward-pass planning from extracted render-frame data.
    /// </summary>
    public class DirectX11RenderPlanBuilderTests {
        /// <summary>
        /// Ensures the builder emits the expected forward pass order when depth prepass, shadows, transparency, and post-process are required.
        /// </summary>
        [Fact]
        public void Build_WhenFrameRequiresDepthShadowsTransparencyAndPostProcess_EmitsExpectedPassOrder() {
            InitializeCore();
            DirectX11RenderPlanBuilder builder = new DirectX11RenderPlanBuilder();
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.DepthPrepassMode = DepthPrepassMode.Always;
            camera.RenderSettings.PostProcessTier = PostProcessTier.High;
            TestDrawable3D drawable = new TestDrawable3D();
            DirectionalLightComponent light = new DirectionalLightComponent();
            RenderFrame frame = new RenderFrame(
                camera,
                [
                    new RenderFrameDrawableSubmission(
                        drawable,
                        true,
                        new RenderFrameBatchingMetadata(false, false, false))
                ],
                [new RenderFrameLightSubmission(light)],
                [new RenderFrameShadowCasterSubmission(drawable)]);

            RenderPlan plan = builder.Build(frame, DirectX11RenderCapabilityProfile.CreateDefault());

            Assert.Equal(
                [
                    RenderPassKind.DepthPrepass,
                    RenderPassKind.Shadow,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.PostProcess,
                    RenderPassKind.Present
                ],
                plan.Passes);
        }

        /// <summary>
        /// Ensures shadow passes are not scheduled when the frame has shadow casters but no shadow-enabled lights.
        /// </summary>
        [Fact]
        public void Build_WhenFrameHasShadowCastersButNoShadowEnabledLights_SkipsShadowPass() {
            InitializeCore();
            DirectX11RenderPlanBuilder builder = new DirectX11RenderPlanBuilder();
            CameraComponent camera = new CameraComponent();
            TestDrawable3D drawable = new TestDrawable3D();
            PointLightComponent light = new PointLightComponent();
            light.ShadowsEnabled = false;
            RenderFrame frame = new RenderFrame(
                camera,
                [
                    new RenderFrameDrawableSubmission(
                        drawable,
                        false,
                        new RenderFrameBatchingMetadata(false, false, false))
                ],
                [new RenderFrameLightSubmission(light)],
                [new RenderFrameShadowCasterSubmission(drawable)]);

            RenderPlan plan = builder.Build(frame, DirectX11RenderCapabilityProfile.CreateDefault());

            Assert.DoesNotContain(RenderPassKind.Shadow, plan.Passes);
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate render queues during the test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Provides one minimal drawable implementation for render-plan tests.
        /// </summary>
        sealed class TestDrawable3D : IDrawable3D {
            /// <summary>
            /// Initializes one test drawable with placeholder runtime resources.
            /// </summary>
            public TestDrawable3D() {
                Model = new TestRuntimeModel();
                Materials = new[] { new TestRuntimeMaterial() };
            }

            /// <summary>
            /// Gets the parent entity that owns the drawable.
            /// </summary>
            public Entity Parent => null;

            /// <summary>
            /// Gets or sets the render order for 3D drawing.
            /// </summary>
            public byte RenderOrder3D { get; set; }

            /// <summary>
            /// Gets the runtime model associated with this drawable.
            /// </summary>
            public RuntimeModel Model { get; }

            /// <summary>
            /// Gets or sets the runtime materials bound to each submesh slot.
            /// </summary>
            public RuntimeMaterial[] Materials { get; set; }
        }
    }
}
