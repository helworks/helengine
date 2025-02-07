using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.sharpdx {
    public class SharpDXWindow : IDisposable {
        public SwapChain Chain;
        public RenderTargetView RenderTarget;

        public void Dispose() {
            Chain.Dispose();
            RenderTarget.Dispose();
        }
    }
}
