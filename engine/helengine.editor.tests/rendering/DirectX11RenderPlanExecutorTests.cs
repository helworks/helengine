using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies ordered DirectX11 render-plan execution for the first planned-runtime slice.
    /// </summary>
    public class DirectX11RenderPlanExecutorTests {
        /// <summary>
        /// Ensures planned geometry passes execute in the same order they were scheduled.
        /// </summary>
        [Fact]
        public void ExecutePlan_WhenPlanContainsDepthOpaqueTransparentAndPresent_InvokesMatchingPassesInOrder() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor();
            RecordingRenderPassExecutor passExecutor = new RecordingRenderPassExecutor();
            DirectX11RenderPassExecutionContext context = CreateExecutionContext(true);
            RenderPlan plan = new RenderPlan(
                [
                    RenderPassKind.DepthPrepass,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.Present
                ]);

            executor.ExecutePlan(context, plan, passExecutor);

            Assert.Equal(
                [
                    RenderPassKind.DepthPrepass,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.Present
                ],
                passExecutor.ExecutedPasses);
        }

        /// <summary>
        /// Ensures transparent execution does not occur when the plan does not include a transparent pass.
        /// </summary>
        [Fact]
        public void ExecutePlan_WhenPlanOmitsTransparentForward_DoesNotInvokeTransparentPass() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor();
            RecordingRenderPassExecutor passExecutor = new RecordingRenderPassExecutor();
            DirectX11RenderPassExecutionContext context = CreateExecutionContext(false);
            RenderPlan plan = new RenderPlan(
                [
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ]);

            executor.ExecutePlan(context, plan, passExecutor);

            Assert.Equal(
                [
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ],
                passExecutor.ExecutedPasses);
        }

        /// <summary>
        /// Ensures shadow and post-process plan entries are tolerated but skipped in this execution slice.
        /// </summary>
        [Fact]
        public void ExecutePlan_WhenPlanContainsShadowAndPostProcess_SkipsUnsupportedPasses() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor();
            RecordingRenderPassExecutor passExecutor = new RecordingRenderPassExecutor();
            DirectX11RenderPassExecutionContext context = CreateExecutionContext(true);
            RenderPlan plan = new RenderPlan(
                [
                    RenderPassKind.Shadow,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.PostProcess,
                    RenderPassKind.Present
                ]);

            executor.ExecutePlan(context, plan, passExecutor);

            Assert.Equal(
                [
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.Present
                ],
                passExecutor.ExecutedPasses);
        }

        /// <summary>
        /// Ensures shadow passes execute when the runtime explicitly enables shadow execution.
        /// </summary>
        [Fact]
        public void ExecutePlan_WhenShadowExecutionIsEnabled_InvokesShadowPass() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor(true, false);
            RecordingRenderPassExecutor passExecutor = new RecordingRenderPassExecutor();
            DirectX11RenderPassExecutionContext context = CreateExecutionContext(false);
            RenderPlan plan = new RenderPlan(
                [
                    RenderPassKind.Shadow,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ]);

            executor.ExecutePlan(context, plan, passExecutor);

            Assert.Equal(
                [
                    RenderPassKind.Shadow,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ],
                passExecutor.ExecutedPasses);
        }

        /// <summary>
        /// Creates one minimal render-pass execution context for plan executor tests.
        /// </summary>
        /// <param name="hasTransparentDrawable">Whether the frame should include a transparent drawable submission.</param>
        /// <returns>Execution context containing one camera and one swap-chain surface placeholder.</returns>
        DirectX11RenderPassExecutionContext CreateExecutionContext(bool hasTransparentDrawable) {
            InitializeCore();
            CameraComponent camera = new CameraComponent();
            RenderFrame frame = new RenderFrame(
                camera,
                CreateDrawableSubmissions(hasTransparentDrawable),
                Array.Empty<RenderFrameLightSubmission>(),
                Array.Empty<RenderFrameShadowCasterSubmission>());
            return new DirectX11RenderPassExecutionContext(frame, new DirectX11SwapChainSurface());
        }

        /// <summary>
        /// Creates drawable submissions for the execution-context test fixture.
        /// </summary>
        /// <param name="hasTransparentDrawable">Whether a transparent submission should be included after the opaque one.</param>
        /// <returns>Drawable submissions used by the execution context.</returns>
        RenderFrameDrawableSubmission[] CreateDrawableSubmissions(bool hasTransparentDrawable) {
            TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);
            if (!hasTransparentDrawable) {
                return [
                    new RenderFrameDrawableSubmission(
                        opaqueDrawable,
                        false,
                        new RenderFrameBatchingMetadata(false, false, false))
                ];
            }

            TestDrawable3D transparentDrawable = new TestDrawable3D(MaterialBlendMode.AlphaBlend);
            return [
                new RenderFrameDrawableSubmission(
                    opaqueDrawable,
                    false,
                    new RenderFrameBatchingMetadata(false, false, false)),
                new RenderFrameDrawableSubmission(
                    transparentDrawable,
                    true,
                    new RenderFrameBatchingMetadata(false, false, false))
            ];
        }

        /// <summary>
        /// Initializes a core instance so camera components can allocate render queues during the test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Records planned pass invocations without touching GPU state.
        /// </summary>
        sealed class RecordingRenderPassExecutor : IDirectX11RenderPassExecutor {
            /// <summary>
            /// Initializes one empty pass recorder.
            /// </summary>
            public RecordingRenderPassExecutor() {
                ExecutedPasses = new List<RenderPassKind>();
            }

            /// <summary>
            /// Gets the ordered pass list recorded during execution.
            /// </summary>
            public List<RenderPassKind> ExecutedPasses { get; }

            /// <summary>
            /// Records one depth-prepass execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecuteDepthPrepass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.DepthPrepass, context);
            }

            /// <summary>
            /// Records one shadow-pass execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecuteShadowPass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.Shadow, context);
            }

            /// <summary>
            /// Records one opaque-forward execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecuteOpaqueForwardPass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.OpaqueForward, context);
            }

            /// <summary>
            /// Records one transparent-forward execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecuteTransparentForwardPass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.TransparentForward, context);
            }

            /// <summary>
            /// Records one post-process execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecutePostProcessPass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.PostProcess, context);
            }

            /// <summary>
            /// Records one present-pass execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public void ExecutePresentPass(DirectX11RenderPassExecutionContext context) {
                Record(RenderPassKind.Present, context);
            }

            /// <summary>
            /// Records one executed pass after validating the supplied context.
            /// </summary>
            /// <param name="passKind">Pass kind to record.</param>
            /// <param name="context">Execution context supplied by the plan executor.</param>
            void Record(RenderPassKind passKind, DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }

                ExecutedPasses.Add(passKind);
            }
        }

        /// <summary>
        /// Minimal drawable used to populate render frames for executor tests.
        /// </summary>
        sealed class TestDrawable3D : IDrawable3D {
            /// <summary>
            /// Initializes one test drawable with the requested blend mode.
            /// </summary>
            /// <param name="blendMode">Material blend mode exposed by the runtime material.</param>
            public TestDrawable3D(MaterialBlendMode blendMode) {
                Model = new TestRuntimeModel();
                RuntimeMaterial material = new RuntimeMaterial();
                material.SetRenderState(new MaterialRenderState {
                    BlendMode = blendMode
                });
                Material = material;
            }

            /// <summary>
            /// Gets the parent entity for the drawable.
            /// </summary>
            public Entity Parent => null;

            /// <summary>
            /// Gets or sets the render order.
            /// </summary>
            public byte RenderOrder3D { get; set; }

            /// <summary>
            /// Gets the runtime model.
            /// </summary>
            public RuntimeModel Model { get; }

            /// <summary>
            /// Gets or sets the runtime material.
            /// </summary>
            public RuntimeMaterial Material { get; set; }

            /// <summary>
            /// Gets the runtime materials bound to each submesh slot.
            /// </summary>
            public RuntimeMaterial[] Materials => Material == null ? Array.Empty<RuntimeMaterial>() : new[] { Material };
        }
    }
}
