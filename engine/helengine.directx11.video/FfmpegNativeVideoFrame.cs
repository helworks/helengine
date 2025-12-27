using System.Runtime.InteropServices;

namespace helengine.directx11.video {
    /// <summary>
    /// Represents a decoded frame returned by the native decoder.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FfmpegNativeVideoFrame {
        /// <summary>
        /// Gets or sets the native ID3D11Texture2D pointer for the frame.
        /// </summary>
        public IntPtr Texture;

        /// <summary>
        /// Gets or sets the subresource index for planar textures.
        /// </summary>
        public int SubresourceIndex;

        /// <summary>
        /// Gets or sets the frame width in pixels.
        /// </summary>
        public int Width;

        /// <summary>
        /// Gets or sets the frame height in pixels.
        /// </summary>
        public int Height;

        /// <summary>
        /// Gets or sets the pixel format for the frame.
        /// </summary>
        public VideoFrameFormat FrameFormat;

        /// <summary>
        /// Gets or sets the presentation timestamp in ticks.
        /// </summary>
        public long TimestampTicks;

        /// <summary>
        /// Gets or sets the frame duration in ticks.
        /// </summary>
        public long DurationTicks;
    }
}
