namespace helengine.directx11.video {
    /// <summary>
    /// Specifies how the decoder should use hardware acceleration when opening a stream.
    /// </summary>
    public enum VideoDecoderHardwareMode {
        /// <summary>
        /// Require hardware acceleration and fail if it is unavailable.
        /// </summary>
        RequireHardware,
        /// <summary>
        /// Prefer hardware acceleration but allow software decoding as a fallback.
        /// </summary>
        PreferHardware,
        /// <summary>
        /// Disable hardware acceleration and force software decoding.
        /// </summary>
        DisableHardware
    }
}
