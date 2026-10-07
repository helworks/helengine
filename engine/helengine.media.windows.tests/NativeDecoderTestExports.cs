using System.Runtime.InteropServices;
namespace helengine.media.windows.tests;
/// <summary>Exercises the public native ABI independently of managed exception mapping.</summary>
public static class NativeDecoderTestExports {
    /// <summary>Returns an explicit native error when an invalid handle or output pointer is supplied.</summary>
    [DllImport("helengine.video.ffmpeg",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)]
    public static extern int he_video_decoder_try_get_frame(IntPtr decoder,IntPtr frame);
}
