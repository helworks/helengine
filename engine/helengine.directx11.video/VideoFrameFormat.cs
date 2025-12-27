namespace helengine.directx11.video {
    /// <summary>
    /// Defines the pixel format of decoded video frames.
    /// </summary>
    public enum VideoFrameFormat {
        /// <summary>
        /// The format is unknown or not supplied by the decoder.
        /// </summary>
        Unknown,
        /// <summary>
        /// NV12 4:2:0 format with 8-bit luma and interleaved UV plane.
        /// </summary>
        Nv12,
        /// <summary>
        /// RGBA format with 8 bits per channel.
        /// </summary>
        Rgba8
    }
}
