using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 post-process chain planning against shared camera and render-target contracts.
    /// </summary>
    public class DirectX11PostProcessChainTests {
        /// <summary>
        /// Ensures disabled post-processing returns no post-process passes.
        /// </summary>
        [Fact]
        public void Build_WhenPostProcessTierIsDisabled_ReturnsNoPasses() {
            DirectX11PostProcessChain chain = new DirectX11PostProcessChain();
            CameraRenderSettings renderSettings = new CameraRenderSettings();
            TestRenderTarget renderTarget = new TestRenderTarget();
            renderSettings.PostProcessTier = PostProcessTier.Disabled;

            DirectX11PostProcessPass[] passes = chain.Build(renderSettings, renderTarget);

            Assert.Empty(passes);
        }

        /// <summary>
        /// Ensures post-processing requires one render target that can be sampled as a texture.
        /// </summary>
        [Fact]
        public void Build_WhenRenderTargetCannotBeSampled_Throws() {
            DirectX11PostProcessChain chain = new DirectX11PostProcessChain();
            CameraRenderSettings renderSettings = new CameraRenderSettings();
            TestRenderTarget renderTarget = new TestRenderTarget {
                CanSampleAsTexture = false
            };
            renderSettings.PostProcessTier = PostProcessTier.High;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(delegate {
                chain.Build(renderSettings, renderTarget);
            });

            Assert.Contains("sampled", exception.Message);
        }
    }
}
