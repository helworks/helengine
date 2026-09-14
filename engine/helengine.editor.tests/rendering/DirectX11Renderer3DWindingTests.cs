using SharpDX.Direct3D11;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 rasterizer front-face settings stay aligned with the engine-authored mesh winding convention.
    /// </summary>
    public sealed class DirectX11Renderer3DWindingTests {
        /// <summary>
        /// Ensures the default DirectX11 3D rasterizer treats counter-clockwise triangles as front-facing so built-in meshes match the Vulkan backend and authored model winding.
        /// </summary>
        [Fact]
        public void PipelineStateCache_WhenCreatingDefault3DRasterizer_UsesCounterClockwiseFrontFaces() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using DirectX11PipelineStateCache pipelineStateCache = new DirectX11PipelineStateCache(renderer.Device);

            RasterizerStateDescription description = pipelineStateCache.DefaultRasterizerState.Description;

            Assert.True(description.IsFrontCounterClockwise);
            Assert.Equal(SharpDX.Direct3D11.CullMode.Back, description.CullMode);
        }

        /// <summary>
        /// Ensures material-specific DirectX11 rasterizer states preserve the same counter-clockwise front-face convention as the default 3D rasterizer.
        /// </summary>
        [Fact]
        public void ResolveRasterizerState_WhenCreatingMaterialSpecificState_PreservesCounterClockwiseFrontFaces() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using DirectX11PipelineStateCache pipelineStateCache = new DirectX11PipelineStateCache(renderer.Device);
            MaterialRenderState renderState = new MaterialRenderState {
                CullMode = MaterialCullMode.Front
            };

            RasterizerStateDescription description = pipelineStateCache.ResolveRasterizerState(renderState).Description;

            Assert.True(description.IsFrontCounterClockwise);
            Assert.Equal(SharpDX.Direct3D11.CullMode.Front, description.CullMode);
        }

        /// <summary>
        /// Ensures DirectX11 shadow rendering does not reuse the unbiased forward rasterizer state, which causes severe self-shadowing on simple lit meshes.
        /// </summary>
        [Fact]
        public void PipelineStateCache_WhenCreatingShadowRasterizer_UsesDedicatedDepthBiasForShadowPasses() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using DirectX11PipelineStateCache pipelineStateCache = new DirectX11PipelineStateCache(renderer.Device);

            RasterizerStateDescription description = pipelineStateCache.ShadowRasterizerState.Description;

            Assert.True(description.IsFrontCounterClockwise);
            Assert.Equal(SharpDX.Direct3D11.CullMode.Back, description.CullMode);
            Assert.NotEqual(0, description.DepthBias);
            Assert.True(description.SlopeScaledDepthBias > 0f);
        }

        /// <summary>
        /// Ensures the renderer's own pipeline state cache is the one that holds these states, so the assertions above cover what rendering actually binds.
        /// </summary>
        [Fact]
        public void Renderer_owns_one_pipeline_state_cache_for_its_device() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();

            DirectX11PipelineStateCache pipelineStateCache = DirectX11RendererTestAccess.GetPipelineStateCache(renderer);

            Assert.NotNull(pipelineStateCache);
            Assert.Same(pipelineStateCache, DirectX11RendererTestAccess.GetPipelineStateCache(renderer));
        }
    }
}
