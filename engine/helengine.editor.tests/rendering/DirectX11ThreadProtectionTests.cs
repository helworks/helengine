using helengine.directx11;
using SharpDX.Direct3D11;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the managed DirectX11 renderer enables the D3D11 runtime's immediate-context thread protection.
    /// </summary>
    public sealed class DirectX11ThreadProtectionTests {
        /// <summary>
        /// Ensures the renderer turns on immediate-context multithread protection during device initialization.
        /// </summary>
        [Fact]
        public void DirectX11_renderer_enables_immediate_context_multithread_protection() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Multithread multithread = renderer.Device.ImmediateContext.QueryInterface<Multithread>();

            Assert.True(multithread.GetMultithreadProtected());
        }
    }
}
