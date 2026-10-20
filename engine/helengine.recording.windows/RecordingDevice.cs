using System.Runtime.InteropServices;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
namespace helengine.recording.windows;
/// <summary>Selects NVIDIA hardware or retains a compatible device supplied by the host.</summary>
sealed class RecordingDevice : IRecordingDevice {
    /// <summary>Owns exactly one device reference independent from the host.</summary>
    readonly Device Device;
    /// <summary>Adopts an already retained device and records its real adapter identity.</summary>
    RecordingDevice(Device device) {
        Device = device;
        using var dxgi = device.QueryInterface<SharpDX.DXGI.Device>(); using var adapter = dxgi.Adapter;
        if (adapter.Description.VendorId != 0x10de || (device.CreationFlags & DeviceCreationFlags.SingleThreaded) != 0) { throw new NotSupportedException("Recording requires a multithread-capable NVIDIA D3D11 device."); }
        Name = adapter.Description.Description.TrimEnd('\0'); Luid = adapter.Description.Luid;
    }
    /// <summary>Borrowed shared device pointer.</summary>
    public IntPtr Handle => Device.NativePointer;
    /// <summary>Selected adapter name for diagnostics.</summary>
    public string Name { get; }
    /// <summary>Selected adapter LUID for diagnostics.</summary>
    public long Luid { get; }
    /// <summary>Imports a valid device or opens the first NVIDIA hardware adapter.</summary>
    internal static IRecordingDevice Open(IntPtr supplied) {
        Device device = null;
        try {
            if (supplied != IntPtr.Zero) { Marshal.AddRef(supplied); device = new Device(supplied); }
            else {
                using var factory = new Factory1(); var adapters = factory.Adapters1;
                try {
                    foreach (var adapter in adapters) {
                        if (adapter.Description1.VendorId == 0x10de && (adapter.Description1.Flags & AdapterFlags.Software) == 0) { device = new Device(adapter, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport); break; }
                    }
                } finally { foreach (var adapter in adapters) { adapter.Dispose(); } }
                if (device == null) { throw new NotSupportedException("No NVIDIA hardware adapter is available for required hevc_nvenc recording."); }
            }
            return new RecordingDevice(device);
        } catch { device?.Dispose(); throw; }
    }
    /// <summary>Releases this session's reference without consuming the host's original ownership.</summary>
    public void Dispose() => Device.Dispose();
}
