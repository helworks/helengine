using System.Runtime.InteropServices;
namespace helengine.directx11.video;
/// <summary>Encoding ABI additions to the shared backend; decode/probe bindings remain separate.</summary>
static class HevcEncoderNativeApi {
    /// <summary>Creates a GPU encoder retaining its own reference to the supplied device.</summary>
    [DllImport("helengine.video.ffmpeg", EntryPoint = "he_video_encoder_create", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int Create(IntPtr device, string path, ref HevcEncoderNativeOptions options, out IntPtr encoder, ref HevcEncoderNativeError error);
    /// <summary>Copies/converts borrowed BGRA input to bounded encoder-owned surfaces before returning.</summary>
    [DllImport("helengine.video.ffmpeg", EntryPoint = "he_video_encoder_submit", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int Submit(HevcEncoderHandle encoder, IntPtr texture, long index, ref HevcEncoderNativeError error);
    /// <summary>Drains packets and writes the MP4 trailer.</summary>
    [DllImport("helengine.video.ffmpeg", EntryPoint = "he_video_encoder_complete", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int Complete(HevcEncoderHandle encoder, ref HevcEncoderNativeError error);
    /// <summary>Aborts an unfinished encoder and frees all native resources.</summary>
    [DllImport("helengine.video.ffmpeg", EntryPoint = "he_video_encoder_destroy", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void Destroy(IntPtr encoder);
}
