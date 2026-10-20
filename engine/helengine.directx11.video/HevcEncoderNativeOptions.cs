using System.Runtime.InteropServices;
namespace helengine.directx11.video;
/// <summary>Mirrors the versioned 32-byte native options with eight-byte packing.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct HevcEncoderNativeOptions {
    /// <summary>ABI structure size used to reject mismatched callers.</summary>
    internal uint Size;
    /// <summary>Unpadded input width.</summary>
    internal int SourceWidth;
    /// <summary>Unpadded input height.</summary>
    internal int SourceHeight;
    /// <summary>Target bit rate, aligned at offset 16.</summary>
    internal long Bitrate;
    /// <summary>Maximum bit rate, aligned at offset 24.</summary>
    internal long MaxBitrate;
    /// <summary>Copies validated managed options into the native layout.</summary>
    internal static HevcEncoderNativeOptions Create(HevcVideoEncoderOptions options) => new() { Size = 32, SourceWidth = options.SourceWidth, SourceHeight = options.SourceHeight, Bitrate = options.Bitrate, MaxBitrate = options.MaxBitrate };
}
