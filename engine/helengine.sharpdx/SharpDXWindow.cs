using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.sharpdx {
    public class SharpDXWindow : IDisposable {
        public D3DDevice Device;
        public SwapChain SwapChain;
        public Texture2D Target;
        public RenderTargetView TargetView;

        public void Dispose() {
            Device.Dispose();
            SwapChain.Dispose();
            Target.Dispose();
            TargetView.Dispose();
        }
    }
}
