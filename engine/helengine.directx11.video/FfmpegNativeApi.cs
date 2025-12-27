using System.Runtime.InteropServices;

namespace helengine.directx11.video {
    /// <summary>
    /// Provides P/Invoke access to the native FFmpeg-backed decoder implementation.
    /// </summary>
    internal static class FfmpegNativeApi {
        /// <summary>
        /// Defines the shared library name expected to host the decoder exports.
        /// </summary>
        const string LibraryName = "helengine.video.ffmpeg";

        /// <summary>
        /// Creates a new native decoder bound to the supplied Direct3D device.
        /// </summary>
        /// <param name="d3d11Device">Native ID3D11Device pointer.</param>
        /// <param name="sourcePath">UTF-16 path to the source file.</param>
        /// <param name="hardwareMode">Hardware usage preference.</param>
        /// <param name="streamInfo">Receives stream metadata when creation succeeds.</param>
        /// <returns>Native decoder handle or IntPtr.Zero on failure.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern IntPtr he_video_decoder_create(IntPtr d3d11Device, [MarshalAs(UnmanagedType.LPWStr)] string sourcePath, VideoDecoderHardwareMode hardwareMode, out FfmpegNativeVideoStreamInfo streamInfo);

        /// <summary>
        /// Destroys a native decoder created by the library.
        /// </summary>
        /// <param name="decoder">Native decoder handle.</param>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void he_video_decoder_destroy(IntPtr decoder);

        /// <summary>
        /// Attempts to decode the next frame.
        /// </summary>
        /// <param name="decoder">Native decoder handle.</param>
        /// <param name="frame">Receives decoded frame metadata when available.</param>
        /// <returns>Non-zero when a frame is produced.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int he_video_decoder_try_get_frame(IntPtr decoder, out FfmpegNativeVideoFrame frame);

        /// <summary>
        /// Releases a decoded frame back to the native decoder.
        /// </summary>
        /// <param name="decoder">Native decoder handle.</param>
        /// <param name="frame">Frame metadata to release.</param>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void he_video_decoder_release_frame(IntPtr decoder, ref FfmpegNativeVideoFrame frame);

        /// <summary>
        /// Seeks the decoder to the requested timestamp.
        /// </summary>
        /// <param name="decoder">Native decoder handle.</param>
        /// <param name="timestampTicks">Target timestamp in ticks.</param>
        /// <returns>Non-zero when the seek succeeds.</returns>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int he_video_decoder_seek(IntPtr decoder, long timestampTicks);

        /// <summary>
        /// Clears buffered decode state after a seek or configuration change.
        /// </summary>
        /// <param name="decoder">Native decoder handle.</param>
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void he_video_decoder_flush(IntPtr decoder);
    }
}
