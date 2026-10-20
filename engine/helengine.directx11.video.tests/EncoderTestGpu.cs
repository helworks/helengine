using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;

namespace helengine.directx11.video.tests;
/// <summary>Creates only synthetic GPU resources on the NVIDIA adapter for encoder tests.</summary>
static class EncoderTestGpu {
    /// <summary>Opens the same NVIDIA hardware class required by the recording profile.</summary>
    internal static Device Open() {
        using var factory = new Factory1();
        foreach (var adapter in factory.Adapters1) {
            using (adapter) {
                if (adapter.Description1.VendorId == 0x10de && (adapter.Description1.Flags & AdapterFlags.Software) == 0) {
                    return new Device(adapter, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);
                }
            }
        }
        throw new InvalidOperationException("NVIDIA GPU is required; hardware tests cannot be approved by skipping them.");
    }
    /// <summary>Builds a GPU-only solid red test texture whose caller controls its lifetime.</summary>
    internal static Texture2D Texture(Device device) {
        var texture = new Texture2D(device, new Texture2DDescription { Width = 640, Height = 480, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0), BindFlags = BindFlags.RenderTarget, Usage = ResourceUsage.Default });
        using var target = new RenderTargetView(device, texture);
        device.ImmediateContext.ClearRenderTargetView(target, new RawColor4(1, 0, 0, 1));
        return texture;
    }
}
