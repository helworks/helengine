using System.Runtime.InteropServices;
namespace helengine.directx11.video;
/// <summary>Preserves the original HRESULT or FFmpeg code from a specific encoding operation.</summary>
public sealed class HevcVideoEncoderException : ExternalException {
    /// <summary>Original signed native error code, also available as HResult.</summary>
    public int NativeCode => HResult;
    /// <summary>Creates an operation failure without replacing its native diagnostic.</summary>
    internal HevcVideoEncoderException(int code, string message) : base(message, code) { }
}
