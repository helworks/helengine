using System.Runtime.InteropServices;

namespace helengine.directx11.video {
    /// <summary>
    /// Mirrors the native stream metadata returned by the FFmpeg-backed decoder.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FfmpegNativeVideoStreamInfo {
        /// <summary>
        /// Gets or sets the pixel width of the decoded frames.
        /// </summary>
        public int Width;

        /// <summary>
        /// Gets or sets the pixel height of the decoded frames.
        /// </summary>
        public int Height;

        /// <summary>
        /// Gets or sets the frames-per-second rate for the stream.
        /// </summary>
        public double FrameRate;

        /// <summary>
        /// Gets or sets the stream duration in ticks.
        /// </summary>
        public long DurationTicks;

        /// <summary>
        /// Gets or sets the pixel format produced by the decoder.
        /// </summary>
        public VideoFrameFormat FrameFormat;

        /// <summary>
        /// Gets or sets whether hardware acceleration is active (1) or not (0).
        /// </summary>
        public int IsHardwareAccelerated;
    }
}
