namespace helengine.directx11.video {
    /// <summary>
    /// Describes the decoded video stream properties exposed by the decoder.
    /// </summary>
    public sealed class VideoStreamInfo {
        /// <summary>
        /// Initializes a new stream description.
        /// </summary>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        /// <param name="frameRate">Frames per second for the stream.</param>
        /// <param name="duration">Total stream duration, or zero when unknown.</param>
        /// <param name="frameFormat">Pixel format for decoded frames.</param>
        /// <param name="isHardwareAccelerated">True when frames are produced by hardware decode.</param>
        public VideoStreamInfo(int width, int height, double frameRate, TimeSpan duration, VideoFrameFormat frameFormat, bool isHardwareAccelerated) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
            }

            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
            }

            if (frameRate <= 0.0) {
                throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be greater than zero.");
            }

            if (duration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
            }

            if (frameFormat == VideoFrameFormat.Unknown) {
                throw new ArgumentOutOfRangeException(nameof(frameFormat), "Frame format must be specified.");
            }

            Width = width;
            Height = height;
            FrameRate = frameRate;
            Duration = duration;
            FrameFormat = frameFormat;
            IsHardwareAccelerated = isHardwareAccelerated;
        }

        /// <summary>
        /// Gets the frame width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the frame height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the frames-per-second rate for the stream.
        /// </summary>
        public double FrameRate { get; }

        /// <summary>
        /// Gets the total stream duration.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the pixel format used by decoded frames.
        /// </summary>
        public VideoFrameFormat FrameFormat { get; }

        /// <summary>
        /// Gets a value indicating whether hardware acceleration is active.
        /// </summary>
        public bool IsHardwareAccelerated { get; }
    }
}
