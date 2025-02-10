using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace helengine.sharpdx {
    public class SharpDXWindow : IDisposable {
        public SwapChain Chain;
        public RenderTargetView RenderTarget;
        public DepthStencilView DepthView;

        public int Width;
        public int Height;

        public void Dispose() {
            Chain.Dispose();
            RenderTarget.Dispose();
            DepthView.Dispose();
        }
    }
}
