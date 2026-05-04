using System.Runtime.CompilerServices;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the DirectX11 renderer prepares atlas-shadow shader state from extracted frame context.
    /// </summary>
    public class DirectX11RendererShadowBindingTests {
        /// <summary>
        /// Ensures the renderer forwards atlas-shadow data into the shadow shader binding seam.
        /// </summary>
        [Fact]
        public void PrepareShadowShaderState_WhenAtlasAllocationsExist_PacksAndBindsShadowShaderData() {
            InitializeCore();
            CameraComponent camera = CreateCamera();
            Entity directionalEntity = CreateEntity(new float3(0f, 0f, 0f));
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalEntity.AddComponent(directionalLight);
            RenderFrameLightSubmission directionalSubmission = new RenderFrameLightSubmission(directionalLight, 10);
            DirectX11ShadowResourceSet shadowResourceSet = new DirectX11ShadowResourceSet(
                [directionalSubmission],
                [new DirectX11ShadowAtlasAllocation(directionalSubmission, 0, 0, 1024, 1024)],
                Array.Empty<DirectX11PointShadowResource>(),
                2048,
                2048);
            RenderFrame frame = new RenderFrame(
                camera,
                Array.Empty<RenderFrameDrawableSubmission>(),
                [directionalSubmission],
                Array.Empty<RenderFrameShadowCasterSubmission>());
            DirectX11RenderPassExecutionContext context = new DirectX11RenderPassExecutionContext(
                frame,
                new DirectX11SwapChainSurface(),
                [directionalSubmission],
                [directionalSubmission],
                shadowResourceSet.AtlasAllocations,
                shadowResourceSet.PointShadowResources);
            ShadowBindingCaptureRenderer renderer = ShadowBindingCaptureRenderer.Create();

            renderer.PrepareShadowShaderStateForTest(context, shadowResourceSet);

            Assert.True(renderer.ShadowAtlasWasBound);
            Assert.Equal(1f, renderer.LastShadowShaderData.ShadowMetadata.X);
            Assert.Equal(1f, renderer.LastShadowShaderData.Light0Metadata.X);
        }

        /// <summary>
        /// Ensures the renderer forwards point-shadow resource presence into the cube-shadow binding seam.
        /// </summary>
        [Fact]
        public void PrepareShadowShaderState_WhenPointShadowResourcesExist_BindsCubeShadowResources() {
            InitializeCore();
            CameraComponent camera = CreateCamera();
            Entity pointEntity = CreateEntity(new float3(0f, 0f, 0f));
            PointLightComponent pointLight = new PointLightComponent();
            pointLight.ShadowsEnabled = true;
            pointEntity.AddComponent(pointLight);
            RenderFrameLightSubmission pointSubmission = new RenderFrameLightSubmission(pointLight, 10);
            DirectX11ShadowResourceSet shadowResourceSet = new DirectX11ShadowResourceSet(
                [pointSubmission],
                Array.Empty<DirectX11ShadowAtlasAllocation>(),
                [new DirectX11PointShadowResource(pointSubmission, 512)],
                0,
                0);
            RenderFrame frame = new RenderFrame(
                camera,
                Array.Empty<RenderFrameDrawableSubmission>(),
                [pointSubmission],
                Array.Empty<RenderFrameShadowCasterSubmission>());
            DirectX11RenderPassExecutionContext context = new DirectX11RenderPassExecutionContext(
                frame,
                new DirectX11SwapChainSurface(),
                [pointSubmission],
                [pointSubmission],
                shadowResourceSet.AtlasAllocations,
                shadowResourceSet.PointShadowResources);
            ShadowBindingCaptureRenderer renderer = ShadowBindingCaptureRenderer.Create();

            renderer.PrepareShadowShaderStateForTest(context, shadowResourceSet);

            Assert.False(renderer.ShadowAtlasWasBound);
            Assert.Equal(1, renderer.LastPointShadowResourceCount);
            Assert.Equal(2f, renderer.LastShadowShaderData.Light0Metadata.Z);
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
        /// Creates a camera attached to an initialized entity.
        /// </summary>
        /// <returns>Camera ready for shadow-binding tests.</returns>
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
        /// DirectX11-shaped renderer that captures shadow shader data without touching GPU state.
        /// </summary>
        sealed class ShadowBindingCaptureRenderer : TestDirectX11RenderManager3D {
            /// <summary>
            /// Gets the last packed shadow shader data prepared by the renderer.
            /// </summary>
            public DirectX11ShadowShaderData LastShadowShaderData { get; private set; }

            /// <summary>
            /// Gets whether the renderer attempted to bind an atlas shadow resource.
            /// </summary>
            public bool ShadowAtlasWasBound { get; private set; }

            /// <summary>
            /// Gets the last bound point-shadow resource count.
            /// </summary>
            public int LastPointShadowResourceCount { get; private set; }

            /// <summary>
            /// Creates one uninitialized renderer recorder instance.
            /// </summary>
            /// <returns>Renderer recorder that can prepare shadow shader state without constructing DirectX11.</returns>
            public new static ShadowBindingCaptureRenderer Create() {
                ShadowBindingCaptureRenderer renderer = (ShadowBindingCaptureRenderer)RuntimeHelpers.GetUninitializedObject(typeof(ShadowBindingCaptureRenderer));
                renderer.LastShadowShaderData = new DirectX11ShadowShaderData();
                renderer.ShadowAtlasWasBound = false;
                renderer.LastPointShadowResourceCount = 0;
                return renderer;
            }

            /// <summary>
            /// Exposes the protected shadow-shader preparation path for tests.
            /// </summary>
            /// <param name="context">Execution context whose selected lights should be packed.</param>
            /// <param name="shadowResourceSet">Planned shadow resources for the current frame.</param>
            public void PrepareShadowShaderStateForTest(DirectX11RenderPassExecutionContext context, DirectX11ShadowResourceSet shadowResourceSet) {
                PrepareShadowShaderState(context, shadowResourceSet);
            }

            /// <summary>
            /// Captures the packed shadow shader data instead of uploading it to a real GPU buffer.
            /// </summary>
            /// <param name="data">Packed shadow shader data prepared for the current frame.</param>
            protected override void UpdateShadowShaderData(DirectX11ShadowShaderData data) {
                LastShadowShaderData = data;
            }

            /// <summary>
            /// Captures whether an atlas shadow resource would have been bound.
            /// </summary>
            /// <param name="atlasWasAvailable">Whether the current frame prepared an atlas shadow resource.</param>
            protected override void UpdateShadowAtlasBindings(bool atlasWasAvailable) {
                ShadowAtlasWasBound = atlasWasAvailable;
            }

            /// <summary>
            /// Captures the number of point-shadow cube resources that would have been bound.
            /// </summary>
            /// <param name="pointShadowResourceCount">Number of point-shadow resources available for the current frame.</param>
            protected override void UpdatePointShadowBindings(int pointShadowResourceCount) {
                LastPointShadowResourceCount = pointShadowResourceCount;
            }
        }
    }
}
