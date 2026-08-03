using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiFactory1 = SharpDX.DXGI.Factory1;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// A headless Direct3D11 device with no swap chain, used to run VFX effect shaders offline.
    /// </summary>
    public sealed class DirectX11VfxDevice : IDisposable {
        public D3DDevice Device { get; }

        public DirectX11VfxDevice() {
            Adapter1 adapter;
            using (var factory = new DxgiFactory1()) {
                adapter = factory.GetAdapter1(0);
            }

            using (adapter) {
                Device = new D3DDevice(adapter, DeviceCreationFlags.None, new[] {
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                    FeatureLevel.Level_10_0
                });
            }
        }

        public void Dispose() {
            Device.Dispose();
        }
    }
}
