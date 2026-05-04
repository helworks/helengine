using System.Runtime.CompilerServices;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies that the live DirectX11 renderer routes camera rendering through extracted-frame planning.
    /// </summary>
    public class DirectX11RendererPlannedExecutionTests {
        /// <summary>
        /// Ensures a camera with opaque and transparent drawables produces the expected planned pass order.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenTransparentDrawableExists_BuildsOpaqueTransparentAndPresentPlan() {
            InitializeCore();
            RecordingPlannedRenderer renderer = RecordingPlannedRenderer.Create();
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.AlphaBlend));

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Equal(
                [
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.TransparentForward,
                    RenderPassKind.Present
                ],
                renderer.LastPlannedPasses);
        }

        /// <summary>
        /// Ensures depth-prepass camera settings affect the live render-plan path before pass execution.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenDepthPrepassIsEnabled_BuildsDepthOpaqueAndPresentPlan() {
            InitializeCore();
            RecordingPlannedRenderer renderer = RecordingPlannedRenderer.Create();
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.DepthPrepassMode = DepthPrepassMode.Always;
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Equal(
                [
                    RenderPassKind.DepthPrepass,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ],
                renderer.LastPlannedPasses);
        }

        /// <summary>
        /// Ensures the live renderer selects visible lights under the backend budget before pass execution.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenVisibleLightsExceedBudget_CarriesSelectedLightsIntoExecutionContext() {
            InitializeCore();
            RecordingPlannedRenderer renderer = RecordingPlannedRenderer.Create(new RendererBackendCapabilityProfile(true, false, true, true, 2, 1));
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));

            Entity firstLightEntity = new Entity();
            firstLightEntity.InitComponents();
            DirectionalLightComponent lowImportanceLight = new DirectionalLightComponent();
            lowImportanceLight.Intensity = 1.0f;
            lowImportanceLight.ShadowsEnabled = false;
            firstLightEntity.AddComponent(lowImportanceLight);

            Entity secondLightEntity = new Entity();
            secondLightEntity.InitComponents();
            PointLightComponent highestImportanceLight = new PointLightComponent();
            highestImportanceLight.Intensity = 4.0f;
            highestImportanceLight.ShadowsEnabled = true;
            secondLightEntity.AddComponent(highestImportanceLight);

            Entity thirdLightEntity = new Entity();
            thirdLightEntity.InitComponents();
            SpotLightComponent mediumImportanceLight = new SpotLightComponent();
            mediumImportanceLight.Intensity = 2.0f;
            thirdLightEntity.AddComponent(mediumImportanceLight);

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Equal(2, renderer.LastSelectedLights.Count);
            Assert.Same(highestImportanceLight, renderer.LastSelectedLights[0].Light);
            Assert.Same(mediumImportanceLight, renderer.LastSelectedLights[1].Light);
        }

        /// <summary>
        /// Ensures the live renderer carries selected shadow lights and planned shadow resources into the execution context.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenShadowedLightsAndShadowCastersExist_CarriesShadowResourcesIntoExecutionContext() {
            InitializeCore();
            RecordingPlannedRenderer renderer = RecordingPlannedRenderer.Create(new RendererBackendCapabilityProfile(true, false, true, true, 4, 2));
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));

            Entity directionalLightEntity = new Entity();
            directionalLightEntity.InitComponents();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalLight.Intensity = 2.0f;
            directionalLightEntity.AddComponent(directionalLight);

            Entity pointLightEntity = new Entity();
            pointLightEntity.InitComponents();
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointLight.Intensity = 1.5f;
            pointLightEntity.AddComponent(pointLight);

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Contains(RenderPassKind.Shadow, renderer.LastPlannedPasses);
            Assert.Equal(2, renderer.LastSelectedShadowLights.Count);
            Assert.Single(renderer.LastShadowAtlasAllocations);
            Assert.Single(renderer.LastPointShadowResources);
            Assert.Same(directionalLight, renderer.LastSelectedShadowLights[0].Light);
            Assert.Same(pointLight, renderer.LastSelectedShadowLights[1].Light);
        }

        /// <summary>
        /// Ensures the live renderer path dispatches the shadow pass before opaque rendering when shadows are planned.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenShadowPassIsPlanned_ExecutesShadowOpaqueAndPresentPassesInOrder() {
            InitializeCore();
            ExecutingPlannedRenderer renderer = ExecutingPlannedRenderer.Create(new RendererBackendCapabilityProfile(true, false, true, true, 4, 2));
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));

            Entity lightEntity = new Entity();
            lightEntity.InitComponents();
            DirectionalLightComponent light = new DirectionalLightComponent();
            light.Intensity = 2.0f;
            lightEntity.AddComponent(light);

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Equal(
                [
                    RenderPassKind.Shadow,
                    RenderPassKind.OpaqueForward,
                    RenderPassKind.Present
                ],
                renderer.ExecutedPasses);
        }

        /// <summary>
        /// Ensures point-shadow resources are rendered during the shadow pass before forward shadow-state preparation.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenPointShadowResourcesExist_RendersPointShadowsBeforePreparingShadowShaderState() {
            InitializeCore();
            PointShadowExecutingRenderer renderer = PointShadowExecutingRenderer.Create(new RendererBackendCapabilityProfile(true, false, true, true, 4, 2));
            CameraComponent camera = new CameraComponent();
            camera.RenderSettings.PostProcessTier = PostProcessTier.Disabled;
            camera.RenderQueue3D.Add(new TestDrawable3D(MaterialBlendMode.Opaque));

            Entity pointLightEntity = new Entity();
            pointLightEntity.InitComponents();
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointLight.Intensity = 1.5f;
            pointLightEntity.AddComponent(pointLight);

            renderer.RenderPlannedCamera(new DirectX11SwapChainSurface(), camera);

            Assert.Equal(
                [
                    "point",
                    "prepare"
                ],
                renderer.ShadowExecutionEvents);
        }

        /// <summary>
        /// Initializes a core instance so camera render queues can be allocated during the test.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Minimal drawable used to populate camera render queues for planned-execution tests.
        /// </summary>
        sealed class TestDrawable3D : IDrawable3D {
            /// <summary>
            /// Initializes one test drawable with the requested blend mode.
            /// </summary>
            /// <param name="blendMode">Blend mode exposed by the runtime material.</param>
            public TestDrawable3D(MaterialBlendMode blendMode) {
                Model = new TestRuntimeModel();
                RuntimeMaterial material = new RuntimeMaterial();
                material.SetRenderState(new MaterialRenderState {
                    BlendMode = blendMode
                });
                Material = material;
            }

            /// <summary>
            /// Gets the parent entity for this drawable.
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
        }

        /// <summary>
        /// DirectX11-shaped renderer that records planned pass order without touching GPU state.
        /// </summary>
        sealed class RecordingPlannedRenderer : TestDirectX11RenderManager3D {
            /// <summary>
            /// Initializes one renderer recorder with no planned passes.
            /// </summary>
            RecordingPlannedRenderer() {
                LastPlannedPasses = Array.Empty<RenderPassKind>();
                LastSelectedLights = Array.Empty<RenderFrameLightSubmission>();
                LastSelectedShadowLights = Array.Empty<RenderFrameLightSubmission>();
                LastShadowAtlasAllocations = Array.Empty<DirectX11ShadowAtlasAllocation>();
                LastPointShadowResources = Array.Empty<DirectX11PointShadowResource>();
            }

            /// <summary>
            /// Gets the last pass order built by the renderer.
            /// </summary>
            public IReadOnlyList<RenderPassKind> LastPlannedPasses { get; private set; }

            /// <summary>
            /// Gets the visible lights selected for the last planned camera render.
            /// </summary>
            public IReadOnlyList<RenderFrameLightSubmission> LastSelectedLights { get; private set; }

            /// <summary>
            /// Gets the shadow-enabled lights selected for the last planned camera render.
            /// </summary>
            public IReadOnlyList<RenderFrameLightSubmission> LastSelectedShadowLights { get; private set; }

            /// <summary>
            /// Gets the planned atlas allocations for the last planned camera render.
            /// </summary>
            public IReadOnlyList<DirectX11ShadowAtlasAllocation> LastShadowAtlasAllocations { get; private set; }

            /// <summary>
            /// Gets the planned point-shadow resources for the last planned camera render.
            /// </summary>
            public IReadOnlyList<DirectX11PointShadowResource> LastPointShadowResources { get; private set; }

            /// <summary>
            /// Gets or sets the capability profile published by the test renderer.
            /// </summary>
            RendererBackendCapabilityProfile CapabilityProfile { get; set; }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance.
            /// </summary>
            /// <returns>Renderer recorder that can execute the planned camera path without constructing DirectX11.</returns>
            public new static RecordingPlannedRenderer Create() {
                return Create(DirectX11RenderCapabilityProfile.CreateDefault());
            }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance with the requested backend capability profile.
            /// </summary>
            /// <param name="capabilityProfile">Backend capability profile used for planning during the test.</param>
            /// <returns>Renderer recorder that can execute the planned camera path without constructing DirectX11.</returns>
            public static RecordingPlannedRenderer Create(RendererBackendCapabilityProfile capabilityProfile) {
                RecordingPlannedRenderer renderer = (RecordingPlannedRenderer)RuntimeHelpers.GetUninitializedObject(typeof(RecordingPlannedRenderer));
                renderer.LastPlannedPasses = Array.Empty<RenderPassKind>();
                renderer.LastSelectedLights = Array.Empty<RenderFrameLightSubmission>();
                renderer.LastSelectedShadowLights = Array.Empty<RenderFrameLightSubmission>();
                renderer.LastShadowAtlasAllocations = Array.Empty<DirectX11ShadowAtlasAllocation>();
                renderer.LastPointShadowResources = Array.Empty<DirectX11PointShadowResource>();
                renderer.CapabilityProfile = capabilityProfile;
                return renderer;
            }

            /// <summary>
            /// Exposes the protected planned camera path for the test surface and camera.
            /// </summary>
            /// <param name="surface">Placeholder output surface.</param>
            /// <param name="camera">Camera whose render queue should be extracted and planned.</param>
            public void RenderPlannedCamera(DirectX11SwapChainSurface surface, CameraComponent camera) {
                RenderCamera(surface, camera);
            }

            /// <summary>
            /// Records the planned pass order instead of executing GPU work.
            /// </summary>
            /// <param name="context">Execution context built by the renderer.</param>
            /// <param name="plan">Ordered plan built by the renderer.</param>
            protected override void ExecuteCameraPlan(DirectX11RenderPassExecutionContext context, RenderPlan plan) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                } else if (plan == null) {
                    throw new ArgumentNullException(nameof(plan));
                }

                LastPlannedPasses = plan.Passes.ToArray();
                LastSelectedLights = context.SelectedLights.ToArray();
                LastSelectedShadowLights = context.SelectedShadowLights.ToArray();
                LastShadowAtlasAllocations = context.ShadowAtlasAllocations.ToArray();
                LastPointShadowResources = context.PointShadowResources.ToArray();
            }

            /// <summary>
            /// Gets the capability profile requested for the test renderer.
            /// </summary>
            /// <returns>Capability profile used to build the test render plan.</returns>
            public override RendererBackendCapabilityProfile GetCapabilityProfile() {
                return CapabilityProfile;
            }
        }

        /// <summary>
        /// DirectX11-shaped renderer that executes the planned pass path while recording pass order instead of touching GPU state.
        /// </summary>
        sealed class ExecutingPlannedRenderer : TestDirectX11RenderManager3D {
            /// <summary>
            /// Initializes one runtime execution recorder with no executed passes.
            /// </summary>
            ExecutingPlannedRenderer() {
                ExecutedPasses = new List<RenderPassKind>();
            }

            /// <summary>
            /// Gets the pass order executed through the live renderer path.
            /// </summary>
            public List<RenderPassKind> ExecutedPasses { get; private set; }

            /// <summary>
            /// Gets or sets the capability profile published by the test renderer.
            /// </summary>
            RendererBackendCapabilityProfile CapabilityProfile { get; set; }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance with the requested backend capability profile.
            /// </summary>
            /// <param name="capabilityProfile">Backend capability profile used for planning during the test.</param>
            /// <returns>Renderer recorder that executes pass dispatch without constructing DirectX11.</returns>
            public static ExecutingPlannedRenderer Create(RendererBackendCapabilityProfile capabilityProfile) {
                ExecutingPlannedRenderer renderer = (ExecutingPlannedRenderer)RuntimeHelpers.GetUninitializedObject(typeof(ExecutingPlannedRenderer));
                renderer.ExecutedPasses = new List<RenderPassKind>();
                renderer.CapabilityProfile = capabilityProfile;
                return renderer;
            }

            /// <summary>
            /// Exposes the protected planned camera path for the test surface and camera.
            /// </summary>
            /// <param name="surface">Placeholder output surface.</param>
            /// <param name="camera">Camera whose render queue should be extracted and executed.</param>
            public void RenderPlannedCamera(DirectX11SwapChainSurface surface, CameraComponent camera) {
                RenderCamera(surface, camera);
            }

            /// <summary>
            /// Suppresses GPU preparation so the live pass executor can run in a test-only environment.
            /// </summary>
            /// <param name="context">Execution context built by the renderer.</param>
            protected override void PrepareCameraFrame(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }
            }

            /// <summary>
            /// Records shadow-pass execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public override void ExecuteShadowPass(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }

                ExecutedPasses.Add(RenderPassKind.Shadow);
            }

            /// <summary>
            /// Records opaque-forward execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public override void ExecuteOpaqueForwardPass(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }

                ExecutedPasses.Add(RenderPassKind.OpaqueForward);
            }

            /// <summary>
            /// Records present-pass execution.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public override void ExecutePresentPass(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }

                ExecutedPasses.Add(RenderPassKind.Present);
            }

            /// <summary>
            /// Gets the capability profile requested for the test renderer.
            /// </summary>
            /// <returns>Capability profile used to build the test render plan.</returns>
            public override RendererBackendCapabilityProfile GetCapabilityProfile() {
                return CapabilityProfile;
            }
        }

        /// <summary>
        /// DirectX11-shaped renderer that exercises the live shadow-pass path while recording point-shadow execution order.
        /// </summary>
        sealed class PointShadowExecutingRenderer : TestDirectX11RenderManager3D {
            /// <summary>
            /// Initializes one point-shadow execution recorder with no captured events.
            /// </summary>
            PointShadowExecutingRenderer() {
                ShadowExecutionEvents = new List<string>();
            }

            /// <summary>
            /// Gets the ordered shadow execution events captured from the live shadow pass.
            /// </summary>
            public List<string> ShadowExecutionEvents { get; private set; }

            /// <summary>
            /// Gets or sets the capability profile published by the test renderer.
            /// </summary>
            RendererBackendCapabilityProfile CapabilityProfile { get; set; }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance with the requested backend capability profile.
            /// </summary>
            /// <param name="capabilityProfile">Backend capability profile used for planning during the test.</param>
            /// <returns>Renderer recorder that executes the live shadow pass without constructing DirectX11.</returns>
            public static PointShadowExecutingRenderer Create(RendererBackendCapabilityProfile capabilityProfile) {
                PointShadowExecutingRenderer renderer = (PointShadowExecutingRenderer)RuntimeHelpers.GetUninitializedObject(typeof(PointShadowExecutingRenderer));
                renderer.ShadowExecutionEvents = new List<string>();
                renderer.CapabilityProfile = capabilityProfile;
                return renderer;
            }

            /// <summary>
            /// Exposes the protected planned camera path for the test surface and camera.
            /// </summary>
            /// <param name="surface">Placeholder output surface.</param>
            /// <param name="camera">Camera whose render queue should be extracted and executed.</param>
            public void RenderPlannedCamera(DirectX11SwapChainSurface surface, CameraComponent camera) {
                RenderCamera(surface, camera);
            }

            /// <summary>
            /// Suppresses GPU preparation so the live pass executor can run in a test-only environment.
            /// </summary>
            /// <param name="context">Execution context built by the renderer.</param>
            protected override void PrepareCameraFrame(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }
            }

            /// <summary>
            /// Records point-shadow rendering instead of touching GPU resources.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
            protected override void RenderPointShadowResources(DirectX11RenderPassExecutionContext context, DirectX11ShadowResourceSet shadowResourceSet) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                } else if (shadowResourceSet == null) {
                    throw new ArgumentNullException(nameof(shadowResourceSet));
                }

                ShadowExecutionEvents.Add("point");
            }

            /// <summary>
            /// Records shadow-state preparation instead of touching GPU resources.
            /// </summary>
            /// <param name="context">Execution context containing selected forward and shadow lights.</param>
            /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
            protected override void PrepareShadowShaderState(DirectX11RenderPassExecutionContext context, DirectX11ShadowResourceSet shadowResourceSet) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                } else if (shadowResourceSet == null) {
                    throw new ArgumentNullException(nameof(shadowResourceSet));
                }

                ShadowExecutionEvents.Add("prepare");
            }

            /// <summary>
            /// Suppresses opaque rendering because this test only exercises the shadow-pass flow.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public override void ExecuteOpaqueForwardPass(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }
            }

            /// <summary>
            /// Suppresses present execution because this test only exercises the shadow-pass flow.
            /// </summary>
            /// <param name="context">Execution context for the pass.</param>
            public override void ExecutePresentPass(DirectX11RenderPassExecutionContext context) {
                if (context == null) {
                    throw new ArgumentNullException(nameof(context));
                }
            }

            /// <summary>
            /// Gets the capability profile requested for the test renderer.
            /// </summary>
            /// <returns>Capability profile used to build the test render plan.</returns>
            public override RendererBackendCapabilityProfile GetCapabilityProfile() {
                return CapabilityProfile;
            }
        }
    }
}
