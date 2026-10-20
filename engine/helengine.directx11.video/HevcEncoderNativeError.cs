using System.Runtime.InteropServices;
using System.Text;
namespace helengine.directx11.video;
/// <summary>Caller-owned native code and bounded UTF-8 text, isolated from other instances.</summary>
[StructLayout(LayoutKind.Sequential)]
struct HevcEncoderNativeError {
    /// <summary>Original native HRESULT or libav error code.</summary>
    internal int Code;
    /// <summary>UTF-8 bytes ending with zero; never marshalled through the ANSI code page.</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] internal byte[] Message;
    /// <summary>Allocates the inline message storage needed by P/Invoke.</summary>
    internal static HevcEncoderNativeError Create() => new() { Message = new byte[1024] };
    /// <summary>Turns this operation's preserved diagnostic into a typed managed exception.</summary>
    internal void Throw() {
        int length = Array.IndexOf(Message, (byte)0);
        throw new HevcVideoEncoderException(Code, Encoding.UTF8.GetString(Message, 0, length < 0 ? Message.Length : length));
    }
}
